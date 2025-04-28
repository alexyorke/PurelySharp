using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Collections.Immutable;
using System;
using System.IO;
using PurelySharp.Analyzer.Engine.Rules; // <-- Add this using
using PurelySharp.Attributes; // Added for PureAttribute
using System.Threading;

namespace PurelySharp.Analyzer.Engine
{
    /// <summary>
    /// Contains the core logic for determining method purity using Control Flow Graph (CFG).
    /// </summary>
    internal static class PurityAnalysisEngine
    {
        // Define a consistent format for symbol comparison strings
        private static readonly SymbolDisplayFormat _signatureFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeContainingType |
                // SymbolDisplayMemberOptions.IncludeExplicitInterfaceImplementation | // Removed for netstandard2.0
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeModifiers, // Keep modifiers for now, might need removal
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeParamsRefOut | // Include ref/out/params
                SymbolDisplayParameterOptions.IncludeDefaultValue, // Include default value
                                                                   // SymbolDisplayParameterOptions.IncludeOptionalLocations, // Removed for netstandard2.0
                                                                   // Explicitly EXCLUDE parameter names:
                                                                   // SymbolDisplayParameterOptions.IncludeName,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier // Include nullable ?
        );

        // --- Updated list of Purity Rules ---
        private static readonly ImmutableList<IPurityRule> _purityRules = ImmutableList.Create<IPurityRule>(
            new AssignmentPurityRule(),
            new MethodInvocationPurityRule(),
            new ConstructorInitializerPurityRule(), // <-- ADDED RULE
            new ReturnStatementPurityRule(),
            new BinaryOperationPurityRule(),
            new PropertyReferencePurityRule(),
            new ArrayElementReferencePurityRule(),
            new CollectionExpressionPurityRule(),
            new ArrayCreationPurityRule(),
            new ArrayInitializerPurityRule(),
            new InterpolatedStringPurityRule(),
            new SwitchStatementPurityRule(),
            new SwitchExpressionPurityRule(),
            new ConstantPatternPurityRule(),
            new DiscardPatternPurityRule(),
            new LoopPurityRule(),
            new FlowCapturePurityRule(),
            new ExpressionStatementPurityRule(),
            new UsingStatementPurityRule(),
            new ParameterReferencePurityRule(),
            new LocalReferencePurityRule(),
            new FieldReferencePurityRule(),
            new BranchPurityRule(),
            new SwitchCasePurityRule(),
            new LiteralPurityRule(),
            new ConversionPurityRule(),
            new FlowCaptureReferencePurityRule(),
            new ConditionalOperationPurityRule(),
            new UnaryOperationPurityRule(),
            new ObjectCreationPurityRule(),
            new CoalesceOperationPurityRule(),
            new ConditionalAccessPurityRule(),
            new ThrowOperationPurityRule(),
            new IsPatternPurityRule(),
            new IsNullPurityRule(),
            new StructuralPurityRule(),
            new TuplePurityRule(),
            new TypeOfPurityRule(),
            new YieldReturnPurityRule(),
            new DelegateCreationPurityRule(),
            new WithOperationPurityRule(),
            new InstanceReferencePurityRule(),
            new ObjectOrCollectionInitializerPurityRule(),
            new LockStatementPurityRule()
        );

        // --- Add list of known impure namespaces ---
        private static readonly ImmutableHashSet<string> KnownImpureNamespaces = ImmutableHashSet.Create(
            StringComparer.Ordinal, // Use ordinal comparison for namespace names
            "System.IO",
            "System.Net",
            "System.Data",
            "System.Threading", // Note: Tasks might be okay if awaited result is pure
            "System.Diagnostics", // Debug, Trace, Process
            "System.Security.Cryptography",
            "System.Runtime.InteropServices",
            "System.Reflection" // Reflection can often lead to impure actions
        );

        // --- Add list of specific impure types ---
        private static readonly ImmutableHashSet<string> KnownImpureTypeNames = ImmutableHashSet.Create(
             StringComparer.Ordinal,
            "System.Random",
            "System.DateTime", // Now, UtcNow properties are impure
            "System.Guid",     // NewGuid() is impure
            "System.Console",
            "System.Environment", // Accessing env variables, etc.
            "System.Timers.Timer"
        // "System.Text.StringBuilder" // REMOVED: Handled by method checks
        // Add specific types like File, HttpClient, Thread etc. if needed beyond namespace check
        );

        /// <summary>
        /// Represents the result of a purity analysis.
        /// </summary>
        public readonly struct PurityAnalysisResult
        {
            /// <summary>
            /// Indicates whether the analyzed element is considered pure.
            /// </summary>
            public bool IsPure { get; }

            /// <summary>
            /// The syntax node of the first operation determined to be impure, if any.
            /// Null if the element is pure or if the specific impure node couldn't be determined.
            /// </summary>
            public SyntaxNode? ImpureSyntaxNode { get; }

            // Private constructor to enforce usage of factory methods
            private PurityAnalysisResult(bool isPure, SyntaxNode? impureSyntaxNode)
            {
                IsPure = isPure;
                ImpureSyntaxNode = impureSyntaxNode;
            }

            /// <summary>
            /// Represents a pure result.
            /// </summary>
            public static PurityAnalysisResult Pure => new PurityAnalysisResult(true, null);

            /// <summary>
            /// Creates an impure result with the specific syntax node causing the impurity.
            /// </summary>
            public static PurityAnalysisResult Impure(SyntaxNode impureSyntaxNode)
            {
                // Ensure we don't pass null here, use the specific overload if syntax is unknown
                if (impureSyntaxNode == null)
                {
                    throw new ArgumentNullException(nameof(impureSyntaxNode), "Use ImpureUnknownLocation for impurity without a specific node.");
                }
                return new PurityAnalysisResult(false, impureSyntaxNode);
            }

            /// <summary>
            /// Creates an impure result where the specific location is unknown or not applicable.
            /// </summary>
            public static PurityAnalysisResult ImpureUnknownLocation => new PurityAnalysisResult(false, null);
        }

        // Add a set of known impure method signatures
        private static readonly HashSet<string> KnownImpureMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            // --- Impure / Unknown (Assume Impure) from list ---
            "System.Activator.CreateInstance<T>()",
            "System.Activator.CreateInstance(System.Type)",
            "System.Activator.CreateInstance(System.Type, params object[])",
            "System.AppContext.GetData(string)",
            "System.Array.Reverse(System.Array)",
            "System.Array.Sort(System.Array)",
            "System.Buffer.BlockCopy(System.Array, int, System.Array, int, int)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Add(TKey, TValue)",
            "System.Collections.Generic.HashSet<T>.UnionWith(System.Collections.Generic.IEnumerable<T>)",
            "System.Collections.Generic.List<T>.Add(T)",
            "System.Collections.Generic.List<T>.Clear()",
            "System.Collections.Generic.List<T>.ForEach(System.Action<T>)",
            "System.Collections.Generic.List<T>.Insert(int, T)",
            "System.Collections.Generic.List<T>.Remove(T)",
            "System.Collections.Generic.Queue<T>.Dequeue()",
            "System.Collections.Generic.Queue<T>.Enqueue(T)",
            "System.Collections.Generic.Stack<T>.Pop()",
            "System.Collections.Generic.Stack<T>.Push(T)",
            "System.Console.Clear()",
            "System.Console.ReadKey()",
            "System.Console.ReadLine()",
            "System.Console.SetCursorPosition(int, int)",
            "System.Console.Write(string)", // Simplified, catch common overloads
            "System.Console.WriteLine(string)", // Simplified, catch common overloads
            "System.Console.Write(object)",
            "System.Console.WriteLine(object)",
            "System.Console.Write()",
            "System.Console.WriteLine()",
            "System.DateTime.Now.get",
            "System.DateTime.UtcNow.get",
            "System.DateTimeOffset.Now.get",
            "System.DateTimeOffset.UtcNow.get",
            "System.Diagnostics.ActivitySource.StartActivity(string)", // Simplified
            "System.Diagnostics.Debug.WriteLine(string)", // Simplified
            "System.Diagnostics.Debugger.Break()",
            "System.Diagnostics.Process.GetCurrentProcess()",
            "System.Diagnostics.Process.Start(string)",
            "System.Diagnostics.Stopwatch.Elapsed.get",
            "System.Diagnostics.Stopwatch.GetTimestamp()",
            "System.Diagnostics.Stopwatch.Start()",
            "System.Diagnostics.Stopwatch.Stop()",
            "System.Diagnostics.Trace.WriteLine(string)", // Simplified
            "System.Environment.CurrentDirectory.get",
            "System.Environment.CurrentDirectory.set",
            "System.Environment.Exit(int)",
            "System.Environment.GetEnvironmentVariable(string)",
            "System.Environment.GetFolderPath(System.Environment.SpecialFolder)", // Common overload
            "System.Environment.MachineName.get",
            "System.Environment.ProcessorCount.get",
            "System.Environment.SetEnvironmentVariable(string, string)",
            "System.Environment.TickCount.get",
            "System.Environment.TickCount64.get",
            "System.GC.Collect()",
            "System.GC.GetTotalMemory(bool)",
            "System.Globalization.CultureInfo.CurrentCulture.get",
            "System.Globalization.RegionInfo.CurrentRegion.get",
            "System.Guid.NewGuid()",
            "System.IO.Directory.CreateDirectory(string)",
            "System.IO.Directory.Exists(string)",
            "System.IO.DriveInfo.TotalSize.get", // Property access needs type context
            "System.IO.DriveInfo.GetDrives()",
            "System.IO.File.AppendAllText(string, string)",
            "System.IO.File.Delete(string)",
            "System.IO.File.Exists(string)",
            "System.IO.File.ReadAllBytes(string)",
            "System.IO.File.ReadAllText(string)",
            "System.IO.File.WriteAllText(string, string)",
            "System.IO.File.WriteAllBytes(string, byte[])", // Added common overload
            "System.IO.MemoryStream.Write(byte[], int, int)", // Specific overload
            "System.IO.Path.GetRandomFileName()",
            "System.IO.Path.GetTempFileName()",
            "System.IO.Path.GetTempPath()",
            "System.IO.Stream.Flush()",
            "System.IO.Stream.Read(byte[], int, int)", // Common overload
            "System.IO.Stream.Seek(long, System.IO.SeekOrigin)",
            "System.IO.Stream.Write(byte[], int, int)", // Common overload
            "System.IO.StreamReader.ReadLine()",
            "System.IO.StreamReader.StreamReader(System.IO.Stream)", // Constructor tied to stream
            "System.IO.StreamWriter.WriteLine(string)", // Simplified
            "System.IO.StreamWriter.StreamWriter(System.IO.Stream)", // Constructor tied to stream
            "System.IO.StringReader.ReadToEnd()",
            "System.IO.StringWriter.Write(string)", // Simplified
            "System.Lazy<T>.Value.get", // First access runs factory
            "System.Linq.Enumerable.ToArray<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Allocates
            "System.Linq.Enumerable.ToDictionary<TSource, TKey>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TKey>)", // Allocates
            "System.Linq.Enumerable.ToHashSet<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Allocates
            "System.Linq.Enumerable.ToList<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Allocates
            "System.Net.Dns.GetHostEntry(string)",
            "System.Net.Http.HttpClient.GetAsync(string)", // Simplified
            "System.Net.Http.HttpClient.GetStringAsync(string)", // Simplified
            "System.Net.Http.HttpClient.PostAsync(string, System.Net.Http.HttpContent)", // Simplified
            "System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()",
            "System.Net.Sockets.Socket.Connect(System.Net.EndPoint)", // Simplified
            "System.Net.Sockets.Socket.ConnectAsync(System.Net.EndPoint)", // Simplified
            "System.Net.Sockets.Socket.Receive(byte[])", // Simplified
            "System.Net.Sockets.Socket.Send(byte[])", // Simplified
            "System.Net.WebClient.DownloadString(string)", // Simplified
            "System.Random.Next()",
            "System.Random.Next(int)",
            "System.Random.NextDouble()",
            "System.Reflection.Assembly.Load(string)",
            "System.Reflection.Assembly.LoadFrom(string)",
            "System.Reflection.FieldInfo.SetValue(object, object)",
            "System.Reflection.MethodBase.GetCurrentMethod()", // Context dependent
            "System.Reflection.MethodInfo.Invoke(object, object[])",
            "System.Reflection.PropertyInfo.SetValue(object, object)",
            "System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)", // Non-deterministic hash
            "System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(System.RuntimeTypeHandle)",
            "System.Runtime.GCSettings.IsServerGC.get",
            "System.Runtime.InteropServices.Marshal.AllocHGlobal(System.IntPtr)",
            "System.Runtime.InteropServices.Marshal.FreeHGlobal(System.IntPtr)",
            "System.Runtime.InteropServices.Marshal.StructureToPtr(object, System.IntPtr, bool)",
            // "System.Runtime.InteropServices.Methods decorated with [DllImport]" -> Assume impure unless marked Pure
            "System.Security.Cryptography.RandomNumberGenerator.GetBytes(byte[])",
            "System.Text.Json.JsonSerializer.DeserializeAsync", // All overloads
            "System.Text.Json.JsonSerializer.SerializeAsync", // All overloads
            "System.Text.StringBuilder.Append(string)", // Simplified, common overloads
            "System.Text.StringBuilder.Append(char)",
            "System.Text.StringBuilder.Append(object)",
            "System.Text.StringBuilder.AppendLine(string)", // Simplified
            "System.Text.StringBuilder.Clear()",
            "System.Text.StringBuilder.EnsureCapacity(int)",
            "System.Text.StringBuilder.Insert(int, string)", // Simplified
            "System.Text.StringBuilder.Remove(int, int)",
            "System.Text.StringBuilder.Replace(string, string)", // Simplified
            "System.Threading.Interlocked.CompareExchange(ref int, int, int)", // Add common overloads
            "System.Threading.Interlocked.CompareExchange(ref long, long, long)",
            "System.Threading.Interlocked.CompareExchange(ref object, object, object)",
            "System.Threading.Interlocked.Increment(ref int)",
            "System.Threading.Interlocked.Increment(ref long)",
            "System.Threading.Interlocked.Decrement(ref int)",
            "System.Threading.Interlocked.Decrement(ref long)",
            "System.Threading.Interlocked.Add(ref int, int)",
            "System.Threading.Interlocked.Add(ref long, long)",
            "System.Threading.Interlocked.Exchange(ref int, int)",
            "System.Threading.Interlocked.Exchange(ref long, long)",
            "System.Threading.Interlocked.Exchange(ref object, object)",
            "System.Threading.Monitor.Enter(object)",
            "System.Threading.Monitor.Exit(object)",
            "System.Threading.Monitor.Pulse(object)",
            "System.Threading.Monitor.Wait(object)",
            "System.Threading.Mutex.ReleaseMutex()",
            "System.Threading.Mutex.WaitOne()",
            "System.Threading.SemaphoreSlim.Release()",
            "System.Threading.SemaphoreSlim.Wait()",
            "System.Threading.Tasks.Task.Delay(int)",
            "System.Threading.Tasks.Task.Delay(System.TimeSpan)",
            "System.Threading.Tasks.Task.Run(System.Action)", // Simplified
            // "System.Threading.Tasks.Task.WhenAll(...)" // Impurity depends on tasks
            // "System.Threading.Tasks.Task.WhenAny(...)" // Impurity depends on tasks
            "System.Threading.Tasks.Task.Yield()",
            "System.Threading.Thread.CurrentThread.get",
            "System.Threading.Thread.ManagedThreadId.get",
            "System.Threading.Thread.Sleep(int)",
            "System.Threading.Thread.Sleep(System.TimeSpan)",
            "System.Threading.Volatile.Write", // All overloads
            "System.TimeZoneInfo.FindSystemTimeZoneById(string)",
            "System.Type.GetType(string)",
            "System.Xml.Linq.XElement.Add(object)", // Simplified
            "System.Xml.Linq.XElement.Load(System.IO.Stream)", // Simplified
            "System.Xml.Linq.XElement.Save(System.IO.Stream)", // Simplified
            "System.Xml.Linq.XNode.Remove()",
            "System.Xml.XmlReader.Create(System.IO.Stream)", // Simplified
            "System.Xml.XmlReader.Read()",
            "System.Xml.XmlWriter.Create(System.IO.Stream)", // Simplified
            "System.Xml.XmlWriter.WriteStartElement(string)", // Simplified
            "System.Xml.XmlWriter.WriteString(string)", // Simplified
            "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>.TryAdd(TKey, TValue)",
            "System.Collections.Concurrent.ConcurrentQueue<T>.Enqueue(T)",
            "System.Collections.Concurrent.ConcurrentQueue<T>.TryDequeue(out T)",
            "System.Collections.Concurrent.BlockingCollection<T>.Add(T)",
            "System.Collections.Concurrent.BlockingCollection<T>.Take()",
            "System.Collections.ObjectModel.ObservableCollection<T>.Add(T)",
            "System.ComponentModel.BackgroundWorker.RunWorkerAsync()",
            "System.Diagnostics.EventLog.WriteEntry(string)", // Simplified
            "System.Diagnostics.PerformanceCounter.NextValue()",
            "System.Diagnostics.TraceSource.TraceEvent(System.Diagnostics.TraceEventType, int)", // Simplified
            "System.Diagnostics.FileVersionInfo.GetVersionInfo(string)",
            "System.Globalization.NumberFormatInfo.CurrentInfo.get",
            "System.IO.Compression.ZipFile.CreateFromDirectory(string, string)", // Simplified
            "System.IO.Compression.ZipFile.ExtractToDirectory(string, string)", // Simplified
            "System.IO.Pipes.NamedPipeServerStream.WaitForConnection()",
            "System.Net.Mail.SmtpClient.Send(System.Net.Mail.MailMessage)", // Simplified
            "System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()",
            "System.Net.NetworkInformation.Ping.Send(string)", // Simplified
            "System.Reflection.Emit.DynamicMethod.DynamicMethod(string, System.Type, System.Type[])", // Simplified
            "System.Reflection.Emit.ILGenerator.Emit(System.Reflection.Emit.OpCode)", // Simplified
            "System.Runtime.Caching.MemoryCache.Default.get",
            "System.Runtime.Caching.MemoryCache.Add(string, object, System.DateTimeOffset)", // Simplified
            "System.Runtime.Caching.MemoryCache.Get(string)", // Simplified
            "System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath(string)",
            "System.Runtime.Serialization.Json.DataContractJsonSerializer.ReadObject(System.IO.Stream)", // Simplified
            "System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject(System.IO.Stream, object)", // Simplified
            "System.Security.Principal.WindowsIdentity.GetCurrent()",
            "System.Security.SecureString.AppendChar(char)",
            "System.Security.SecureString.Dispose()",
            "System.Threading.CancellationToken.Register(System.Action)", // Simplified
            "System.Threading.CancellationToken.ThrowIfCancellationRequested()",
            "System.Threading.CancellationTokenSource.Cancel()",
            "System.Threading.ReaderWriterLockSlim.EnterReadLock()",
            "System.Threading.ReaderWriterLockSlim.ExitReadLock()",
            "System.Threading.SpinWait.SpinOnce()",
            "System.Threading.Timer.Timer(System.Threading.TimerCallback)", // Simplified constructor
            "System.Threading.Timer.Change(int, int)", // Simplified
            "System.Timers.Timer.Start()",
            "System.Timers.Timer.Stop()",
            "System.Xml.Xsl.XslCompiledTransform.Load(string)", // Simplified
            "System.Xml.Xsl.XslCompiledTransform.Transform(string, string)", // Simplified
            "System.Collections.BitArray.Set(int, bool)",
            "System.Collections.Specialized.NameValueCollection.Add(string, string)", // Simplified
            "System.Diagnostics.Process.GetProcessesByName(string)",
            "System.IO.DirectoryInfo.Exists.get", // Property getter
            "System.IO.DirectoryInfo.EnumerateFiles()", // Simplified
            "System.IO.FileInfo.Length.get", // Property getter
            "System.IO.FileSystemWatcher.EnableRaisingEvents.set", // Property setter
            "System.Net.HttpListener.Start()",
            "System.Net.HttpListener.GetContext()",
            "System.Net.Sockets.UdpClient.Receive(ref System.Net.IPEndPoint)", // Simplified
            "System.Reflection.AssemblyName.GetAssemblyName(string)",
            "System.Security.Cryptography.X509Certificates.X509Store.Open(System.Security.Cryptography.X509Certificates.OpenFlags)", // Simplified
            "System.Threading.Barrier.SignalAndWait()",
            "System.Threading.CountdownEvent.Signal()",
            "System.Threading.CountdownEvent.Wait()",
            "System.Threading.Tasks.Dataflow.ActionBlock<TInput>.Post(TInput)",
            "System.Threading.Tasks.Parallel.ForEach", // All overloads
            "System.Threading.Tasks.Parallel.Invoke", // All overloads
            "System.Windows.Input.ICommand.Execute(object)", // Interface method
            "Microsoft.Extensions.Logging.ILogger.LogInformation(string)", // Simplified
            "Microsoft.Extensions.DependencyInjection.ServiceProvider.GetService(System.Type)", // Simplified
            "System.IO.BufferedStream.BufferedStream(System.IO.Stream)", // Constructor tied to stream
            "System.IO.BufferedStream.Flush()",

            // --- Added from third list (Impure) ---
            "Microsoft.Extensions.Configuration.IConfiguration.GetConnectionString(string)",
            "Microsoft.Extensions.Configuration.IConfigurationRoot.Reload()",
            "System.Buffers.ArrayPool<T>.Shared.Rent(int)",
            "System.Buffers.ArrayPool<T>.Shared.Return(T[], bool)",
            "System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(System.Span<byte>, ulong)",
            "System.Buffers.Text.Base64.EncodeToUtf8(System.ReadOnlySpan<byte>, System.Span<byte>, out int, out int)", // Simplified
            "System.Collections.Generic.LinkedList<T>.AddFirst(T)",
            "System.Collections.Generic.LinkedListNode<T>.Value.set", // Property setter
            "System.Collections.Generic.SortedDictionary<TKey, TValue>.Add(TKey, TValue)",
            "System.Collections.ObjectModel.KeyedCollection<TKey, TItem>.Remove(TKey)", // Corrected generic params
            "System.ComponentModel.CancelEventArgs.Cancel.set", // Property setter
            "System.ComponentModel.INotifyPropertyChanged.PropertyChanged", // Event add/remove implicitly impure
            "System.Data.DataTable.NewRow()",
            "System.Data.DataRow.AcceptChanges()",
            "System.Diagnostics.Activity.Current.get", // Property get (ambient context)
            "System.Diagnostics.Activity.Current.set", // Property set (ambient context)
            "System.Diagnostics.Activity.SetTag(string, object)",
            "System.Diagnostics.DiagnosticListener.Write(string, object)",
            "System.Drawing.Bitmap.Bitmap(int, int)", // Constructor (GDI resource)
            "System.IO.Compression.BrotliStream.BrotliStream(System.IO.Stream, System.IO.Compression.CompressionMode)", // Constructor tied to stream
            "System.IO.Compression.DeflateStream.Read(byte[], int, int)", // Simplified Read
            "System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(string)", // Simplified
            "System.IO.MemoryMappedFiles.MemoryMappedViewAccessor.ReadByte(long)", // Simplified
            "System.Linq.Queryable.Count<TSource>(System.Linq.IQueryable<TSource>)", // Executes query
            "System.Linq.Queryable.ToList<TSource>(System.Linq.IQueryable<TSource>)", // Executes query
            "System.Net.Http.Headers.HttpRequestHeaders.Add(string, string)", // Simplified Add
            "System.Net.Security.SslStream.AuthenticateAsClientAsync(string)", // Simplified
            "System.Net.Sockets.Socket.Accept()",
            "System.Net.Sockets.SocketAsyncEventArgs.AcceptSocket.set", // Property setter
            "System.Reflection.Emit.AssemblyBuilder.DefineDynamicModule(string)", // Simplified
            "System.Resources.ResourceManager.GetString(string)",
            "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start<TStateMachine>(ref TStateMachine)", // Simplified
            "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.Add(TKey, TValue)",
            "System.Runtime.InteropServices.ComWrappers.GetOrCreateObjectForComInstance(System.IntPtr, System.Runtime.InteropServices.CreateObjectFlags)", // Simplified
            "System.Runtime.InteropServices.GCHandle.Alloc(object)", // Simplified
            "System.Runtime.InteropServices.GCHandle.Free()",
            "System.Security.AccessControl.DirectorySecurity.AddAccessRule(System.Security.AccessControl.FileSystemAccessRule)", // Simplified
            "System.Security.Cryptography.Pkcs.SignedCms.Sign()", // Simplified
            "System.Security.Cryptography.Xml.SignedXml.ComputeSignature()", // Simplified
            "System.Security.Cryptography.X509Certificates.X509Certificate2.X509Certificate2(string)", // Constructor (File I/O)
            "System.ServiceProcess.ServiceBase.Run(System.ServiceProcess.ServiceBase)", // Simplified
            "System.Speech.Synthesis.SpeechSynthesizer.SpeakAsync(string)",
            "System.Text.Json.Utf8JsonWriter.WriteString(string, string)", // Simplified
            "System.Threading.AsyncLocal<T>.Value.get", // Property get (ambient context)
            "System.Threading.AsyncLocal<T>.Value.set", // Property set (ambient context)
            "System.Threading.Channels.ChannelReader<T>.ReadAsync(System.Threading.CancellationToken)", // Simplified
            "System.Threading.Channels.ChannelWriter<T>.WriteAsync(T, System.Threading.CancellationToken)", // Simplified
            "System.Threading.LazyInitializer.EnsureInitialized<T>(ref T, System.Func<T>)", // Simplified
            "System.Transactions.TransactionScope.TransactionScope()", // Simplified constructor
            "System.Transactions.Transaction.Current.get", // Property get (ambient context)
            "Microsoft.Win32.RegistryKey.OpenSubKey(string)", // Simplified (CurrentUser assumed)
            "Microsoft.Win32.RegistryKey.GetValue(string)",
            "Microsoft.Win32.RegistryKey.SetValue(string, object)",
            "System.Net.Http.Headers.HttpContentHeaders.ContentLength.set", // Property setter
            "System.Runtime.InteropServices.SafeHandle.Dispose()",
            "System.Text.Unicode.Utf8.ToUtf16(System.ReadOnlySpan<byte>, System.Span<char>, out int, out int)", // Simplified
            "System.Threading.Semaphore.Semaphore(int, int)", // Constructor (OS resource)
            "System.TimeZoneInfo.ClearCachedData()",
            "System.AppContext.SetSwitch(string, bool)",
            "System.Collections.Generic.PriorityQueue<TElement, TPriority>.Enqueue(TElement, TPriority)",
            "System.Collections.Generic.PriorityQueue<TElement, TPriority>.Dequeue()",
            "System.Diagnostics.Metrics.Counter<T>.Add(T, System.Collections.Generic.KeyValuePair<string, object?>)", // Simplified
            "System.Runtime.InteropServices.MemoryMarshal.Write<T>(System.Span<byte>, ref T)",
            "System.ComponentModel.Component.Dispose()",
            "System.ComponentModel.LicenseManager.Validate(System.Type, object)",
            "System.Configuration.ConfigurationManager.AppSettings.get", // Property get
            "System.Console.Beep()",
            "System.Console.BufferHeight.get",
            "System.Console.BufferHeight.set",
            "System.Console.Title.get",
            "System.Console.Title.set",
            "System.Data.DataSet.Clear()",
            "System.Diagnostics.Debugger.IsAttached.get",
            "System.Diagnostics.Debugger.Launch()",
            "System.Diagnostics.StackTrace.StackTrace()", // Constructor
            "System.Diagnostics.Switch.Level.get",
            "System.DirectoryServices.DirectoryEntry.DirectoryEntry(string)", // Simplified constructor
            "System.GC.GetGeneration(object)",
            "System.GC.KeepAlive(object)",
            "System.Globalization.DateTimeFormatInfo.CurrentInfo.get",
            "System.IO.BinaryReader.ReadBoolean()", // Simplified
            "System.IO.BinaryWriter.Write(string)", // Simplified
            "System.IO.Directory.EnumerateDirectories(string)", // Simplified
            "System.IO.Directory.GetCurrentDirectory()",
            "System.IO.Directory.SetCurrentDirectory(string)",
            "System.IO.FileStream.FileStream(string, System.IO.FileMode)", // Constructor
            "System.IO.Pipelines.PipeReader.ReadAsync(System.Threading.CancellationToken)", // Simplified
            "System.IO.Pipelines.PipeWriter.WriteAsync(System.ReadOnlyMemory<byte>, System.Threading.CancellationToken)", // Simplified
            "System.Linq.ParallelEnumerable.ForAll<TSource>(System.Linq.ParallelQuery<TSource>, System.Action<TSource>)", // Simplified
            "System.Linq.ParallelQuery<TSource>.ToList()",
            "System.Management.ManagementObjectSearcher.ManagementObjectSearcher(string)", // Simplified constructor
            "System.Net.CredentialCache.DefaultCredentials.get",
            "System.Net.Http.HttpMessageInvoker.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)", // Simplified
            "System.Net.ServicePointManager.SecurityProtocol.get",
            "System.Net.ServicePointManager.SecurityProtocol.set",
            "System.Runtime.InteropServices.Marshal.GetLastWin32Error()",
            "System.Runtime.Serialization.FormatterServices.GetUninitializedObject(System.Type)",
            "System.Threading.ThreadLocal<T>.Value.get",

            // --- Added from fourth list (Impure) ---
            "System.Collections.Generic.IEnumerator<T>.MoveNext()",
            "System.Collections.ObjectModel.Collection<T>.InsertItem(int, T)",
            "System.Collections.ObjectModel.Collection<T>.SetItem(int, T)",
            "System.ComponentModel.INotifyCollectionChanged.CollectionChanged", // Event add/remove
            "System.DateTime.ToLocalTime()",
            "System.Delegate.DynamicInvoke(object[])",
            "System.Environment.OSVersion.get",
            // System.EventHandler<TEventArgs> depends on handler
            "System.GC.SuppressFinalize(object)",
            // System.IConvertible.ToType depends on implementation
            "System.IDisposable.Dispose()",
            "System.IServiceProvider.GetService(System.Type)",
            "System.IO.File.Copy(string, string)", // Simplified
            "System.IO.File.Move(string, string)",
            "System.IO.File.OpenRead(string)",
            "System.IO.File.OpenWrite(string)",
            "System.IO.File.ReadAllLines(string)",
            "System.IO.Stream.Close()",
            "System.IO.Stream.CopyToAsync(System.IO.Stream)", // Simplified
            "System.IO.TextReader.Peek()",
            "System.IO.TextReader.ReadToEnd()",
            "System.IO.TextWriter.Flush()",
            "System.IO.TextWriter.Write(char)", // Simplified
            "System.Net.Http.HttpContent.ReadAsStringAsync()",
            "System.Net.Http.HttpContent.ReadAsByteArrayAsync()",
            "System.Text.Encoding.Default.get", // Property get
            "System.Threading.Tasks.Task.ContinueWith(System.Action<System.Threading.Tasks.Task>)", // Simplified
            "System.Threading.Tasks.Task.Wait()",
            "System.Threading.Tasks.Task<TResult>.Result.get", // Property get (blocks)

            // --- Added from fifth list (Impure) ---
            "System.Array.Clear(System.Array, int, int)",
            "System.Array.ConstrainedCopy(System.Array, int, System.Array, int, int)", // Simplified
            "System.Array.Copy(System.Array, System.Array, int)", // Simplified
            "System.Array.Resize<T>(ref T[], int)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Clear()",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Remove(TKey)",
            "System.Collections.Generic.HashSet<T>.Add(T)",
            "System.Collections.Generic.HashSet<T>.Clear()",
            "System.Collections.Generic.HashSet<T>.Remove(T)",
            "System.Collections.Generic.List<T>.AddRange(System.Collections.Generic.IEnumerable<T>)",
            "System.Collections.Generic.List<T>.Capacity.set", // Property setter
            "System.Collections.Generic.List<T>.InsertRange(int, System.Collections.Generic.IEnumerable<T>)",
            "System.Collections.Generic.List<T>.RemoveAll(System.Predicate<T>)",
            "System.Collections.Generic.List<T>.RemoveAt(int)",
            "System.Collections.Generic.List<T>.RemoveRange(int, int)",
            "System.Collections.Generic.List<T>.Reverse()",
            "System.Collections.Generic.List<T>.Sort()", // Includes overload with Comparison<T>
            "System.Collections.Generic.Queue<T>.Clear()",
            "System.Collections.Generic.Stack<T>.Clear()",
            "System.Exception.Source.set", // Property setter
            "System.IO.Path.GetFullPath(string)",

            // --- Added from sixth list (Impure) ---
            "System.Activator.CreateInstanceFrom(string, string)", // Simplified
            "System.Array.Clear(System.Array, int, int)",
            "System.Array.ConstrainedCopy(System.Array, int, System.Array, int, int)", // Simplified
            "System.Array.Copy(System.Array, System.Array, int)", // Simplified
            "System.Array.Sort<T>(T[], System.Comparison<T>)", // Simplified
            "System.Array.Resize<T>(ref T[], int)",
            "System.Collections.Concurrent.ConcurrentBag<T>.Add(T)",
            "System.Collections.Concurrent.ConcurrentBag<T>.TryTake(out T)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.TryAdd(TKey, TValue)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Values.CopyTo(TValue[], int)", // Simplified
            "System.Collections.Generic.ICollection<T>.Add(T)",
            "System.Collections.Generic.ICollection<T>.Clear()",
            "System.Collections.Generic.ICollection<T>.Remove(T)",
            "System.Collections.Generic.IList<T>.Insert(int, T)",
            "System.Collections.Generic.IList<T>.RemoveAt(int)",
            "System.Collections.Generic.SortedSet<T>.Add(T)",
            "System.ComponentModel.EventHandlerList.AddHandler(object, System.Delegate)", // Simplified
            "System.Console.OpenStandardError()",
            "System.Console.OpenStandardInput()",
            "System.Console.OpenStandardOutput()",
            "System.Console.SetIn(System.IO.TextReader)",
            "System.Console.SetOut(System.IO.TextWriter)",
            "System.Diagnostics.Process.ExitCode.get",
            "System.Environment.CommandLine.get",
            "System.Environment.ProcessId.get",
            "System.Environment.StackTrace.get",
            "System.Environment.SystemDirectory.get",
            "System.HashCode.Add<T>(T)", // Simplified
            "System.IO.DirectoryInfo.Create()",
            "System.IO.DirectoryInfo.Delete()",
            "System.IO.FileInfo.CopyTo(string)", // Simplified
            "System.IO.FileInfo.Delete()",
            "System.IO.Stream.ReadAsync(byte[], int, int, System.Threading.CancellationToken)", // Simplified
            "System.IO.Stream.WriteAsync(byte[], int, int, System.Threading.CancellationToken)", // Simplified
            "System.IO.StreamReader.StreamReader(string)", // Constructor
            "System.IO.StreamWriter.StreamWriter(string)", // Constructor
            "System.Linq.Enumerable.ToLookup", // All overloads (create objects)
            "System.Text.StringBuilder.AppendJoin(string, object[])", // Simplified
            "System.Threading.Monitor.TryEnter(object)", // Simplified

            // --- ValueTask/Task related --- 
            "System.Threading.Tasks.ValueTask<TResult>.ValueTask(TResult)", // Add constructor as impure based on test expectations
            "System.Threading.Tasks.Task.Run(System.Action)", // Task.Run should be impure
            "System.IO.MemoryStream.MemoryStream()" // Corrected Signature: Treat constructor as impure by default
        };

        // Add a set of known PURE BCL method/property signatures (using OriginalDefinition.ToDisplayString() format)
        // This helps handle cases where CFG analysis might fail or be too complex for common BCL members.
        private static readonly HashSet<string> KnownPureBCLMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            // --- Pure / Mostly Pure / Conditionally Pure from list ---
            // System.Array
            "System.Array.ConvertAll<TInput, TOutput>(TInput[], System.Converter<TInput, TOutput>)", // Purity depends on converter
            "System.Array.Empty<T>()",
            "System.Array.Exists<T>(T[], System.Predicate<T>)", // Purity depends on match
            "System.Array.IndexOf(System.Array, object)",
            "System.Array.TrueForAll<T>(T[], System.Predicate<T>)", // Purity depends on match
            "System.Array.Length.get", // Added for completeness

            // System.Attribute
            "System.Attribute.GetCustomAttribute(System.Reflection.MemberInfo, System.Type)", // Simplified

            // System.BitConverter
            "System.BitConverter.GetBytes(int)", // Common overload
            "System.BitConverter.GetBytes(double)", // Common overload
            "System.BitConverter.ToInt32(byte[], int)",
            "System.BitConverter.ToDouble(byte[], int)", // Added common counterpart

            // System.Boolean
            "bool.Parse(string)", // Mostly pure (culture)
            "bool.ToString()", // Pure

            // System.Char
            "char.IsDigit(char)",
            "char.IsLetter(char)",
            "char.IsWhiteSpace(char)",
            "char.ToLowerInvariant(char)",
            "char.ToUpperInvariant(char)",
            "char.ToString()", // Pure

            // System.Collections.Generic
            "System.Collections.Generic.Comparer<T>.Default.get",
            "System.Collections.Generic.Dictionary<TKey, TValue>()", // Constructor
            "System.Collections.Generic.Dictionary<TKey, TValue>.ContainsKey(TKey)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.ContainsValue(TValue)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.TryGetValue(TKey, out TValue)", // Technically impure (out), but often ok
            "System.Collections.Generic.Dictionary<TKey, TValue>.Count.get", // Added common property
            "System.Collections.Generic.Dictionary<TKey, TValue>.Keys.get", // Added common property
            "System.Collections.Generic.Dictionary<TKey, TValue>.Values.get", // Added common property
            "System.Collections.Generic.EqualityComparer<T>.Default.get",
            "System.Collections.Generic.HashSet<T>.IsSubsetOf(System.Collections.Generic.IEnumerable<T>)",
            "System.Collections.Generic.List<T>()", // Constructor
            "System.Collections.Generic.List<T>.Contains(T)",
            "System.Collections.Generic.List<T>.Count.get",
            "System.Collections.Generic.List<T>.Find(System.Predicate<T>)", // Purity depends on match
            "System.Collections.Generic.List<T>.Exists(System.Predicate<T>)", // Added from Array
            "System.Collections.Generic.List<T>.TrueForAll(System.Predicate<T>)", // Added from Array
            "System.Collections.Generic.List<T>.this[int].get", // Added indexer get
            "System.Collections.Generic.Queue<T>.Peek()",
            "System.Collections.Generic.Stack<T>.Peek()",

            // System.Collections.Immutable
            "System.Collections.Immutable.ImmutableList<T>.Add(T)",
            "System.Collections.Immutable.ImmutableList<T>.Contains(T)",
            "System.Collections.Immutable.ImmutableList<T>.Count.get",
            "System.Collections.Immutable.ImmutableList<T>.Remove(T)",
            "System.Collections.Immutable.ImmutableList<T>.SetItem(int, T)",
            // "System.Collections.Immutable.ImmutableList<T>.TryGetValue(...)" // Not a standard method? Maybe indexer?
            "System.Collections.Immutable.ImmutableList<T>.this[int].get", // Assuming indexer get
            "System.Collections.Immutable.ImmutableList.Create<T>()", // Common factories
            "System.Collections.Immutable.ImmutableList.Create<T>(params T[])",
            "System.Collections.Immutable.ImmutableArray.Create<T>()",
            "System.Collections.Immutable.ImmutableArray.Create<T>(params T[])",
            "System.Collections.Immutable.ImmutableDictionary.Create<TKey, TValue>()",
            "System.Collections.Immutable.ImmutableHashSet.Create<T>()",

            // System.ComponentModel
            "System.ComponentModel.TypeDescriptor.GetProperties(object)", // Simplified

            // System.Convert
            "System.Convert.FromBase64String(string)",
            "System.Convert.ToBase64String(byte[])",
            "System.Convert.ToDouble(object)", // Simplified
            "System.Convert.ToInt32(object)", // Simplified
            "System.Convert.ToString(object)", // Simplified

            // System.DateTime / DateTimeOffset
            "System.DateTime.DateTime(long)", // Constructor
            "System.DateTime.DateTime(int, int, int)", // Constructor
            "System.DateTime.AddDays(double)",
            "System.DateTime.IsLeapYear(int)",
            "System.DateTime.ToString()", // Added common method
            "System.DateTimeOffset.DateTimeOffset(long, System.TimeSpan)", // Constructor
            "System.DateTimeOffset.FromUnixTimeMilliseconds(long)",
            "System.DateTimeOffset.ToUnixTimeSeconds()",
            "System.DateTimeOffset.ToString()", // Added common method

            // System.DBNull
            "System.DBNull.Value.get", // Field access

            // System.Diagnostics.Contracts
            "System.Diagnostics.Contracts.Contract.Ensures(bool)", // Intent
            "System.Diagnostics.Contracts.Contract.Requires(bool)", // Intent

            // System.Diagnostics.Stopwatch
            "System.Diagnostics.Stopwatch.Stopwatch()", // Constructor

            // System.Double
            "double.Parse(string)", // Mostly pure (culture)
            "double.ToString()", // Mostly pure (culture)

            // System.Enum
            "System.Enum.GetName(System.Type, object)",
            "System.Enum.GetValues(System.Type)",
            "System.Enum.IsDefined(System.Type, object)",

            // System.Globalization
            "System.Globalization.CultureInfo.GetCultureInfo(string)",
            "System.Globalization.CultureInfo.InvariantCulture.get",

            // System.Guid
            "System.Guid.Guid(byte[])", // Constructor
            "System.Guid.Parse(string)",
            "System.Guid.ToString()", // Added common method

            // System.Int32
            "int.Parse(string)", // Mostly pure (culture)
            "int.ToString()", // Mostly pure (culture)

            // System.IO (Pure subset)
            "System.IO.MemoryStream.ToArray()",
            "System.IO.Path.Combine(string, string)", // Common overload
            "System.IO.Path.GetDirectoryName(string)",
            "System.IO.Path.GetFileName(string)",
            "System.IO.StringReader.StringReader(string)", // Constructor
            "System.IO.StringWriter()", // Constructor

            // System.Lazy<T>
            "System.Lazy<T>.Lazy(System.Func<T>)", // Constructor

            // System.Linq.Enumerable
            "System.Linq.Enumerable.Aggregate", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.All<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)", // Conditionally Pure
            "System.Linq.Enumerable.Any<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.Cast<TResult>(System.Collections.IEnumerable)", // Conditionally Pure
            "System.Linq.Enumerable.Count<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Mostly Pure
            "System.Linq.Enumerable.Empty<TResult>()",
            "System.Linq.Enumerable.FirstOrDefault<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.GroupBy", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.OfType<TResult>(System.Collections.IEnumerable)", // Conditionally Pure
            "System.Linq.Enumerable.OrderBy", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Range(int, int)",
            "System.Linq.Enumerable.Repeat<TResult>(TResult, int)",
            "System.Linq.Enumerable.Select<TSource, TResult>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TResult>)", // Conditionally Pure
            "System.Linq.Enumerable.SequenceEqual<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Collections.Generic.IEnumerable<TSource>)", // Conditionally Pure
            "System.Linq.Enumerable.Sum", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Where<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)", // Conditionally Pure

            // System.Math
            "System.Math.Abs(double)", // Simplified
            "System.Math.Ceiling(double)", // Simplified
            "System.Math.Clamp", // All overloads
            "System.Math.Floor(double)", // Simplified
            "System.Math.Max", // All overloads
            "System.Math.Min", // All overloads
            "System.Math.Round", // All overloads
            "System.Math.Sin(double)",
            "System.Math.Sqrt(double)",

            // System.MemoryExtensions
            "System.MemoryExtensions.SequenceEqual<T>(System.ReadOnlySpan<T>, System.ReadOnlySpan<T>)", // Simplified
            "System.MemoryExtensions.Trim<T>(System.ReadOnlySpan<T>)", // Simplified

            // System.Net (Pure subset)
            "System.Net.Http.HttpClient()", // Constructor
            "System.Net.IPAddress.Loopback.get",
            "System.Net.IPAddress.Parse(string)",
            "System.Net.WebUtility.HtmlEncode(string)",
            "System.Net.WebUtility.UrlDecode(string)",

            // System.Nullable
            "System.Nullable.Compare<T>(T?, T?)",
            "System.Nullable.Equals<T>(T?, T?)",

            // System.Numerics
            "System.Numerics.BigInteger.Add(System.Numerics.BigInteger, System.Numerics.BigInteger)",
            "System.Numerics.BigInteger.Parse(string)",
            "System.Numerics.Complex.Complex(double, double)", // Constructor
            "System.Numerics.Complex.Abs(System.Numerics.Complex)",

            // System.Object
            "object.Equals(object, object)",
            "object.GetHashCode()",
            "object.GetType()",
            "object.ReferenceEquals(object, object)",
            "object.ToString()", // Usually Pure

            // System.OperatingSystem
            "System.OperatingSystem.IsWindows()", // Usually cached

            // System.Reflection (Pure subset)
            "System.Reflection.Assembly.GetExecutingAssembly()", // Usually stable during analysis
            "System.Reflection.Assembly.GetTypes()", // Read metadata
            "System.Reflection.FieldInfo.GetValue(object)", // Reads state, assume pure object state
            "System.Reflection.PropertyInfo.GetValue(object)", // Reads state via getter, assume pure getter
            "System.Reflection.TypeInfo.GetMethods()", // Read metadata
            "System.Reflection.TypeInfo.GetProperties()", // Read metadata

            // System.Runtime.InteropServices (Pure subset)
            "System.Runtime.InteropServices.Marshal.PtrToStructure<T>(System.IntPtr)", // Simplified

            // System.Security.Claims
            "System.Security.Claims.ClaimsPrincipal.IsInRole(string)",

            // System.Security.Cryptography (Pure subset - assuming fixed inputs)
            "System.Security.Cryptography.Aes.DecryptCbc(byte[], byte[], byte[], System.Security.Cryptography.PaddingMode)", // Simplified
            "System.Security.Cryptography.Aes.EncryptCbc(byte[], byte[], byte[], System.Security.Cryptography.PaddingMode)", // Simplified
            "System.Security.Cryptography.MD5.ComputeHash(byte[])", // Simplified
            "System.Security.Cryptography.SHA256.ComputeHash(byte[])", // Simplified

            // System.String
            "string.Concat(string, string)", // Common overload
            "string.Concat(params string[])",
            "string.Format(string, object)", // Common overload
            "string.IsNullOrEmpty(string)",
            "string.IsNullOrWhiteSpace(string)",
            "string.Replace(string, string)",
            "string.Split(char[])", // Common overload
            "string.Substring(int, int)",
            "string.ToLower()",
            "string.ToUpper()",
            "string.Trim()",
            "string.Length.get",
            "string.Equals(string)", // Added for completeness
            "string.Equals(object)", // Added for completeness
            "string.GetHashCode()", // Added for completeness
            "string.ToLowerInvariant()", // Added for completeness
            "string.ToUpperInvariant()", // Added for completeness

            // System.StringComparer
            "System.StringComparer.InvariantCultureIgnoreCase.Compare(string, string)",
            "System.StringComparer.Ordinal.Equals(string, string)",

            // System.Text
            "System.Text.Encoding.GetBytes(string)",
            "System.Text.Encoding.GetEncoding(string)",
            "System.Text.Encoding.GetString(byte[])",
            "System.Text.Encoding.UTF8.get",
            "System.Text.Json.JsonSerializer.Deserialize<TValue>(string, System.Text.Json.JsonSerializerOptions?)", // Mostly Pure (depends on TValue ctors)
            "System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, System.Text.Json.JsonSerializerOptions?)", // Mostly Pure (depends on TValue properties)
            "System.Text.RegularExpressions.Regex.Regex(string)", // Constructor
            "System.Text.RegularExpressions.Regex.IsMatch(string, string)", // Static
            "System.Text.RegularExpressions.Regex.Match(string, string)", // Static
            "System.Text.RegularExpressions.Regex.Replace(string, string, string)", // Static
            "System.Text.RegularExpressions.Regex.Split(string, string)", // Static
            "System.Text.RegularExpressions.Regex.IsMatch(string)", // Instance
            "System.Text.RegularExpressions.Regex.Match(string)", // Instance
            "System.Text.RegularExpressions.Regex.Replace(string, string)", // Instance
            "System.Text.RegularExpressions.Regex.Split(string)", // Instance
            "System.Text.StringBuilder()", // Constructor
            "System.Text.StringBuilder.Capacity.get",
            "System.Text.StringBuilder.ToString()",

            // System.Threading (Pure subset)
            "System.Threading.Tasks.Task.CompletedTask.get",
            "System.Threading.Tasks.Task.FromResult<TResult>(TResult)",
            "System.Threading.Volatile.Read", // All overloads

            // System.TimeSpan
            "System.TimeSpan.TimeSpan(long)", // Constructor
            "System.TimeSpan.Add(System.TimeSpan)",
            "System.TimeSpan.ToString()", // Added common method

            // System.TimeZoneInfo
            "System.TimeZoneInfo.ConvertTime(System.DateTimeOffset, System.TimeZoneInfo)", // Simplified

            // System.Tuple / ValueTuple
            "System.Tuple.Create", // All Create methods
            "System.ValueTuple.Create", // All Create methods

            // System.Type
            "System.Type.Equals(object)", // Added override
            "System.Type.Equals(System.Type)", // Added specific overload
            "System.Type.GetHashCode()", // Added override
            "System.Type.ToString()", // Added override
            // typeof() operator handled separately (it's pure)

            // --- Added from third list (Pure/Mostly Pure/Conditionally Pure) ---
            "System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(System.ReadOnlySpan<byte>)",
            "System.Buffers.Text.Utf8Parser.TryParse(System.ReadOnlySpan<byte>, out int, out int)", // Simplified TryParse
            "System.Collections.Generic.LinkedListNode<T>.Value.get", // Property getter
            "System.Collections.Generic.SortedList<TKey, TValue>.IndexOfKey(TKey)",
            "System.Collections.ObjectModel.KeyedCollection<TKey, TItem>.Contains(TKey)", // Corrected generic params
            "System.ComponentModel.AddingNewEventArgs.AddingNewEventArgs()", // Simplified constructor
            "System.ComponentModel.CancelEventArgs.Cancel.get", // Property getter
            "System.Drawing.Color.FromArgb(int, int, int, int)",
            "System.Drawing.Point.Point(int, int)", // Constructor
            "System.Globalization.StringInfo.ParseCombiningCharacters(string)",
            "System.Linq.IQueryable<T>.Expression.get", // Property getter
            "System.Linq.Queryable.Where<TSource>(System.Linq.IQueryable<TSource>, System.Linq.Expressions.Expression<System.Func<TSource, bool>>)",
            "System.Memory<T>.Span.get", // Property getter
            "System.Memory<T>.Slice(int, int)",
            "System.Net.Sockets.SocketAsyncEventArgs.AcceptSocket.get", // Property getter
            "System.Numerics.Matrix4x4.CreateRotationX(float)",
            "System.Numerics.Vector3.Normalize(System.Numerics.Vector3)",
            "System.Reflection.AssemblyName.AssemblyName(string)", // Constructor
            "System.Reflection.CustomAttributeData.GetCustomAttributes(System.Reflection.MemberInfo)", // Simplified
            "System.Reflection.Metadata.MetadataReader.GetString(System.Reflection.Metadata.StringHandle)", // Simplified
            "System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>(ref TFrom)", // Simplified
            "System.Runtime.CompilerServices.Unsafe.SizeOf<T>()",
            "System.Runtime.Intrinsics.X86.Sse.Add(System.Runtime.Intrinsics.Vector128<float>, System.Runtime.Intrinsics.Vector128<float>)", // Example intrinsic
            "System.Runtime.Intrinsics.X86.Avx2.Multiply(System.Runtime.Intrinsics.Vector256<double>, System.Runtime.Intrinsics.Vector256<double>)", // Example intrinsic
            "System.Runtime.Versioning.FrameworkName.FrameworkName(string)", // Constructor
            "System.Security.Cryptography.Pkcs.SignedCms.Decode(byte[])", // Simplified
            "System.Text.Json.JsonDocument.Parse(string, System.Text.Json.JsonDocumentOptions)", // Mostly Pure
            "System.Text.Json.JsonElement.GetString()",
            "System.Threading.Channels.Channel.CreateUnbounded<T>()", // Simplified
            "System.Xml.Schema.XmlSchemaSet.Compile()",
            "System.Xml.XmlDocument.LoadXml(string)",
            "System.Xml.XmlDocument.SelectSingleNode(string)", // Simplified
            "System.Diagnostics.CounterSample.Calculate(System.Diagnostics.CounterSample, System.Diagnostics.CounterSample)", // Simplified
            "System.Net.Http.Headers.HttpContentHeaders.ContentLength.get", // Property getter
            "System.Numerics.Plane.Normalize(System.Numerics.Plane)",
            "System.Reflection.Emit.Label.Equals(object)", // Example struct method
            "System.Runtime.InteropServices.SafeHandle.IsInvalid.get", // Property getter
            "System.Threading.Tasks.ValueTask.AsTask()",
            "System.Buffers.ReadOnlySequence<T>.Slice(long)", // Simplified
            "System.Diagnostics.Metrics.Meter.CreateCounter<T>(string, string, string)", // Simplified
            "System.IO.Hashing.Crc32.Hash(System.ReadOnlySpan<byte>)", // Simplified
            "System.Linq.Enumerable.Chunk<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)", // Conditionally Pure
            "System.Runtime.InteropServices.MemoryMarshal.Read<T>(System.ReadOnlySpan<byte>)",
            "System.ArgumentNullException.ArgumentNullException(string)", // Constructor
            "System.ArgumentOutOfRangeException.ArgumentOutOfRangeException(string)", // Constructor
            "System.ArraySegment<T>.ArraySegment(T[], int, int)", // Constructor
            "System.Attribute.GetCustomAttributes(System.Reflection.MemberInfo)", // Simplified
            "System.AttributeUsageAttribute.AttributeUsageAttribute(System.AttributeTargets)", // Constructor
            "System.BadImageFormatException.BadImageFormatException(string)", // Constructor
            "System.BitOperations.LeadingZeroCount(uint)",
            "System.BitOperations.PopCount(ulong)",
            "System.CodeDom.Compiler.CodeDomProvider.CreateProvider(string)",
            "System.CodeDom.Compiler.CompilerResults.Errors.get", // Property getter
            "System.Collections.ArrayList.Adapter(System.Collections.IList)",
            "System.Collections.Hashtable.ContainsKey(object)",
            "System.Collections.Queue.Synchronized(System.Collections.Queue)",
            "System.Collections.SortedList.GetKey(int)",
            "System.ComponentModel.AttributeCollection.GetDefaultAttribute<T>()",
            "System.ComponentModel.BrowsableAttribute.BrowsableAttribute(bool)", // Constructor
            "System.ComponentModel.DataAnnotations.RangeAttribute.RangeAttribute(double, double)", // Constructor
            "System.ComponentModel.DescriptionAttribute.DescriptionAttribute(string)", // Constructor
            "System.Data.DataColumn.DataColumn(string)", // Constructor
            "System.Data.DataRelation.DataRelation(string, System.Data.DataColumn, System.Data.DataColumn)", // Constructor
            "System.Diagnostics.ConditionalAttribute.ConditionalAttribute(string)", // Constructor
            "System.Diagnostics.Debug.Assert(bool)", // Intent
            "System.Diagnostics.StackFrame.GetMethod()", // Reads metadata
            "System.Diagnostics.Stopwatch.IsRunning.get", // Property getter
            "System.DivideByZeroException.DivideByZeroException()", // Constructor
            "System.EventArgs.Empty.get", // Field access
            "System.Exception.HResult.get",
            "System.Exception.InnerException.get",
            "System.Exception.ToString()",
            "System.FlagsAttribute.FlagsAttribute()", // Constructor
            "System.FormatException.FormatException(string)", // Constructor
            "System.Globalization.CompareInfo.Compare(string, string)",
            "System.Half.Parse(string)", // Example Half method
            "System.HashCode.Combine<T1, T2>(T1, T2)", // Simplified
            "System.Index.Index(int, bool)", // Constructor
            "System.IO.EndOfStreamException.EndOfStreamException()", // Constructor
            "System.IO.Path.ChangeExtension(string, string)",
            "System.IO.Path.HasExtension(string)",
            "System.IO.Pipelines.Pipe.Pipe(System.IO.Pipelines.PipeOptions)", // Constructor
            "System.Linq.Expressions.Expression.Constant(object)",
            "System.Linq.Expressions.Expression.Call(System.Reflection.MethodInfo, System.Linq.Expressions.Expression[])", // Simplified
            "System.Linq.ParallelEnumerable.AsParallel<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
            "System.MemoryExtensions.AsSpan<T>(T[])",
            "System.MemoryExtensions.BinarySearch<T>(System.ReadOnlySpan<T>, T)", // Simplified
            "System.Net.Cookie.Cookie(string, string)", // Constructor
            "System.Net.HttpVersion.Version11.get", // Field access
            "System.NotImplementedException.NotImplementedException()", // Constructor
            "System.Nullable<T>.GetValueOrDefault()",
            "System.Numerics.Quaternion.Quaternion(float, float, float, float)", // Constructor
            "System.ObsoleteAttribute.ObsoleteAttribute(string)", // Constructor
            "System.OverflowException.OverflowException()", // Constructor
            "System.PlatformNotSupportedException.PlatformNotSupportedException()", // Constructor
            "System.Range.Range(System.Index, System.Index)", // Constructor
            "System.Reflection.Emit.OpCodes.Ldarg_0.get", // Field access
            "System.Reflection.IntrospectionExtensions.GetTypeInfo(System.Type)",
            "System.Reflection.MemberInfo.Name.get",
            "System.Reflection.Missing.Value.get", // Field access
            "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute.CallerArgumentExpressionAttribute(string)", // Constructor
            "System.Runtime.CompilerServices.IsExternalInit", // Attribute Type itself
            "System.Runtime.CompilerServices.MethodImplAttribute.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions)", // Constructor
            "System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(System.Collections.Generic.List<T>)",
            "System.Runtime.Serialization.DataContractAttribute.DataContractAttribute()", // Constructor
            "System.Security.AllowPartiallyTrustedCallersAttribute.AllowPartiallyTrustedCallersAttribute()", // Constructor
            // System.Security.Permissions.SecurityAction enum values are constants
            "System.SerializableAttribute.SerializableAttribute()", // Constructor
            // System.Threading.LockCookie struct methods are value operations
            "System.Threading.ThreadLocal<T>.ThreadLocal(System.Func<T>)", // Constructor
            "System.UIntPtr.UIntPtr(uint)", // Constructor

            // --- Added from fourth list (Pure/Mostly Pure/Conditionally Pure) ---
            "System.Collections.Generic.ICollection<T>.Count.get",
            "System.Collections.Generic.IDictionary<TKey, TValue>.Keys.get",
            "System.Collections.Generic.IDictionary<TKey, TValue>.Values.get",
            "System.Collections.Generic.IEnumerable<T>.GetEnumerator()",
            "System.Collections.Generic.IEnumerator<T>.Current.get",
            "System.Collections.Generic.KeyValuePair<TKey, TValue>.KeyValuePair(TKey, TValue)", // Constructor
            "System.Collections.Generic.KeyValuePair<TKey, TValue>.Key.get",
            "System.Collections.Generic.KeyValuePair<TKey, TValue>.Value.get",
            "System.ComponentModel.DataAnnotations.RequiredAttribute.RequiredAttribute()", // Constructor
            "System.ComponentModel.DataAnnotations.StringLengthAttribute.StringLengthAttribute(int)", // Constructor
            "System.DateTime.CompareTo(System.DateTime)",
            "System.DateTime.Equals(System.DateTime)",
            "System.DateTime.Subtract(System.DateTime)",
            "System.DateTime.ToFileTime()",
            "System.DateTimeOffset.CompareTo(System.DateTimeOffset)",
            "System.DateTimeOffset.Equals(System.DateTimeOffset)",
            "System.DateTimeOffset.Subtract(System.DateTimeOffset)",
            "System.Decimal.Compare(decimal, decimal)",
            "System.Decimal.ToDouble(decimal)",
            "System.Decimal.ToInt32(decimal)",
            "System.Delegate.Combine(System.Delegate, System.Delegate)",
            "System.Delegate.Remove(System.Delegate, System.Delegate)",
            "System.Enum.Parse(System.Type, string)",
            "System.Enum.TryParse<TEnum>(string, out TEnum)", // Simplified
            "System.Environment.NewLine.get",
            // System.Func<TResult> is a type
            // System.Action is a type
            // System.Predicate<T> is a type
            // System.IComparable<T>.CompareTo depends on implementation
            "System.IEquatable<T>.Equals(T)", // Interface method (intended pure)
            // System.IFormatProvider.GetFormat depends on implementation
            "System.Linq.Enumerable.Average", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Distinct<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.ElementAt<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)", // Conditionally Pure
            "System.Linq.Enumerable.First<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.Last<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.Max", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Min", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.OrderByDescending", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Reverse<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Conditionally Pure
            "System.Linq.Enumerable.Single<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // Simplified - Conditionally Pure
            "System.Linq.Enumerable.Skip<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)", // Conditionally Pure
            "System.Linq.Enumerable.Take<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)", // Conditionally Pure
            "System.Linq.Enumerable.ThenBy", // All overloads - Conditionally Pure
            "System.Linq.Enumerable.Zip", // All overloads - Conditionally Pure
            "System.Math.Sign(decimal)", // Simplified
            "System.Math.Truncate(double)",
            "System.Net.Http.HttpRequestMessage.HttpRequestMessage(System.Net.Http.HttpMethod, string)", // Constructor
            "System.Net.Http.HttpResponseMessage.IsSuccessStatusCode.get",
            "System.Net.Http.StringContent.StringContent(string, System.Text.Encoding, string)", // Constructor
            "System.Net.IPEndPoint.IPEndPoint(System.Net.IPAddress, int)", // Constructor
            "System.ObjectDisposedException.ObjectDisposedException(string)", // Constructor
            "System.OperatingSystem.Platform.get",
            "System.Reflection.MemberInfo.GetCustomAttributes(bool)", // Simplified
            "System.Reflection.PropertyInfo.PropertyType.get",
            "System.Runtime.InteropServices.Marshal.SizeOf<T>()",
            "string.Contains(string)",
            "string.EndsWith(string)",
            "string.IndexOf(char)", // Simplified
            "string.Insert(int, string)",
            "string.Join(string, System.Collections.Generic.IEnumerable<string>)", // Simplified
            "string.PadLeft(int)", // Simplified
            "string.Remove(int)", // Simplified
            "string.StartsWith(string)",
            "System.Text.Encoding.ASCII.get",
            "System.Text.StringBuilder.Length.get",
            "System.Threading.CancellationToken.None.get", // Field access
            "System.Threading.Interlocked.Read(ref long)",
            "System.TimeSpan.CompareTo(System.TimeSpan)",
            "System.TimeSpan.FromDays(double)",
            "System.Uri.ToString()",

            // --- Added from fifth list (Pure/Mostly Pure/Conditionally Pure) ---
            "System.AggregateException.AggregateException(System.Collections.Generic.IEnumerable<System.Exception>)", // Simplified constructor
            "System.AggregateException.Flatten()",
            "System.ArgumentException.ArgumentException(string, string)", // Constructor
            "System.Array.AsReadOnly<T>(T[])",
            "System.Array.BinarySearch(System.Array, object)", // Simplified
            "System.Array.Find<T>(T[], System.Predicate<T>)", // Conditionally Pure
            "System.Array.FindIndex<T>(T[], System.Predicate<T>)", // Simplified - Conditionally Pure
            "bool.CompareTo(bool)",
            "bool.TryParse(string, out bool)",
            "byte.Parse(string)", // Mostly Pure
            "byte.TryParse(string, out byte)", // Mostly Pure
            "char.GetNumericValue(char)",
            "char.IsControl(char)",
            "char.IsLower(char)",
            "char.IsNumber(char)",
            "char.IsPunctuation(char)",
            "char.IsSeparator(char)",
            "char.IsSymbol(char)",
            "char.IsUpper(char)",
            "char.ToString(char)",
            "System.Collections.Generic.HashSet<T>.Contains(T)",
            "System.Collections.Generic.List<T>.AsReadOnly()",
            "System.Collections.Generic.List<T>.BinarySearch(T)", // Simplified
            "System.Collections.Generic.List<T>.Capacity.get", // Property getter
            "System.Collections.Generic.List<T>.ConvertAll<TOutput>(System.Converter<T, TOutput>)", // Conditionally Pure
            // "System.Collections.Generic.List<T>.Exists(...)" // Already added
            "System.Collections.Generic.List<T>.FindAll(System.Predicate<T>)", // Conditionally Pure
            "System.Collections.Generic.List<T>.FindIndex(System.Predicate<T>)", // Simplified - Conditionally Pure
            "System.Collections.Generic.List<T>.FindLast(System.Predicate<T>)", // Conditionally Pure
            "System.Collections.Generic.List<T>.IndexOf(T)",
            "System.Collections.Generic.List<T>.LastIndexOf(T)",
            "System.Collections.Generic.List<T>.ToArray()",
            // "System.Collections.Generic.List<T>.TrueForAll(...)" // Already added
            "System.Collections.Generic.Queue<T>.Contains(T)",
            "System.Collections.Generic.Queue<T>.ToArray()",
            "System.Collections.Generic.Stack<T>.Contains(T)",
            "System.Collections.Generic.Stack<T>.ToArray()",
            "System.Convert.ChangeType(object, System.Type)", // Mostly Pure
            "System.DateTime.Day.get",
            "System.DateTime.DayOfWeek.get",
            "System.DateTime.DayOfYear.get",
            "System.DateTime.Hour.get",
            "System.DateTime.Kind.get",
            "System.DateTime.Millisecond.get",
            "System.DateTime.Minute.get",
            "System.DateTime.Month.get",
            "System.DateTime.Second.get",
            "System.DateTime.Ticks.get",
            "System.DateTime.TimeOfDay.get",
            "System.DateTime.ToLongDateString()", // Mostly Pure
            "System.DateTime.ToLongTimeString()", // Mostly Pure

            // --- Added from sixth list (Pure/Mostly Pure/Conditionally Pure) ---
            "System.Array.GetLength(int)",
            "System.Array.IndexOf<T>(T[], T)", // Simplified - Conditionally Pure
            "System.Array.LastIndexOf<T>(T[], T)", // Simplified - Conditionally Pure
            "System.Attribute.IsDefined(System.Reflection.MemberInfo, System.Type)", // Simplified
            "System.Buffers.ReadOnlySequence<T>.End.get",
            "System.Buffers.ReadOnlySequence<T>.IsEmpty.get",
            "System.Buffers.ReadOnlySequence<T>.Length.get",
            "System.Buffers.ReadOnlySequence<T>.Start.get",
            "char.ConvertFromUtf32(int)",
            "char.ConvertToUtf32(char, char)",
            "System.Collections.Generic.Dictionary<TKey, TValue>.Values.get", // Already present
            "System.Collections.Generic.EqualityComparer<T>.Equals(T, T)",
            "System.Collections.Generic.EqualityComparer<T>.GetHashCode(T)",
            "System.Collections.Generic.ICollection<T>.Contains(T)",
            "System.Collections.Generic.IList<T>.IndexOf(T)",
            "System.Collections.Generic.List<T>.Contains(T)", // Already present
            "System.Collections.Generic.Queue<T>.TryPeek(out T)",
            "System.Collections.Generic.SortedSet<T>.GetViewBetween(T, T)",
            "System.ComponentModel.DataAnnotations.EmailAddressAttribute.EmailAddressAttribute()", // Constructor
            "System.ComponentModel.DataAnnotations.RegularExpressionAttribute.RegularExpressionAttribute(string)", // Constructor
            "System.ComponentModel.TypeDescriptor.GetConverter(System.Type)",
            "System.Convert.FromHexString(string)",
            "System.Convert.ToHexString(byte[])",
            "System.Convert.ToInt16(object)", // Simplified
            "System.Convert.ToSingle(object)", // Simplified
            "System.DateTime.Parse(string)", // Mostly Pure
            "System.DateTime.ParseExact(string, string, System.IFormatProvider)", // Simplified
            "System.DateTimeOffset.Parse(string)", // Mostly Pure
            "System.DateTimeOffset.ParseExact(string, string, System.IFormatProvider)", // Simplified
            "decimal.Negate(decimal)",
            "decimal.Parse(string)", // Mostly Pure
            "decimal.TryParse(string, out decimal)", // Mostly Pure
            "System.Diagnostics.ActivitySource.ActivitySource(string, string)", // Constructor
            "System.Diagnostics.DiagnosticListener.DiagnosticListener(string)", // Constructor
            "System.Diagnostics.FileVersionInfo.FileVersion.get",
            "System.Diagnostics.Process.Id.get",
            "System.Diagnostics.Process.StartInfo.get",
            "double.PositiveInfinity.get", // Field access
            "System.FileNotFoundException.FileNotFoundException(string)", // Constructor
            "System.FormattableString.Format.get",
            "System.FormattableString.ToString(System.IFormatProvider)",
            "System.HashCode.HashCode()", // Constructor
            "System.HashCode.ToHashCode()",
            "System.Index.End.get", // Static property
            "System.Index.Start.get", // Static property
            "long.Parse(string)", // Mostly Pure
            "long.TryParse(string, out long)", // Mostly Pure
            "System.InvalidOperationException.InvalidOperationException(string)", // Constructor
            "System.IO.DirectoryInfo.Name.get",
            "System.IO.DirectoryInfo.Parent.get",
            "System.IO.FileInfo.DirectoryName.get",
            "System.IO.FileInfo.Extension.get",
            "System.IO.FileInfo.Name.get",
            // "System.Linq.Enumerable.SequenceEqual<TSource>(...)\" // Already present
            "System.Linq.Enumerable.TakeWhile<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)", // Simplified - Conditionally Pure
            "System.Math.Abs(int)",
            "System.Math.Ceiling(decimal)",
            "System.Net.IPAddress.Any.get", // Field access
            "System.Net.IPAddress.Parse(System.ReadOnlySpan<char>)",
            "System.NotSupportedException.NotSupportedException(string)", // Constructor
            "object.MemberwiseClone()", // Protected method
            "System.Reflection.MethodBase.IsStatic.get",
            "System.Reflection.TypeInfo.IsValueType.get",
            "System.Runtime.InteropServices.MemoryMarshal.AsBytes<T>(System.Span<T>)",
            "string.IsNullOrWhiteSpace(System.ReadOnlySpan<char>)",
            "System.TimeSpan.Zero.get", // Field access

            // System.Xml.Linq (Pure subset)
            "System.Xml.Linq.XDocument.Parse(string)",
            "System.Xml.Linq.XElement.Attribute(System.Xml.Linq.XName)",
            "System.Xml.Linq.XElement.Descendants()", // Simplified
            "System.Xml.Linq.XElement.Elements()", // Simplified
            "System.Xml.Linq.XElement.Value.get", // Added common property
            "System.Xml.Linq.XAttribute.Value.get", // Added common property

            // System.Text.RegularExpressions
            "System.Text.RegularExpressions.Regex.IsMatch(string, string)",
        };

        /// <summary>
        /// Represents the purity state during CFG analysis.
        /// </summary>
        private struct PurityAnalysisState : System.IEquatable<PurityAnalysisState>
        {
            public bool HasPotentialImpurity { get; set; }
            public SyntaxNode? FirstImpureSyntaxNode { get; set; }

            public static PurityAnalysisState Pure => new PurityAnalysisState { HasPotentialImpurity = false, FirstImpureSyntaxNode = null };

            public static PurityAnalysisState Merge(IEnumerable<PurityAnalysisState> states)
            {
                bool mergedImpurity = false;
                SyntaxNode? firstImpureNode = null;
                foreach (var state in states)
                {
                    if (state.HasPotentialImpurity)
                    {
                        mergedImpurity = true;
                        if (firstImpureNode == null) { firstImpureNode = state.FirstImpureSyntaxNode; }
                    }
                }
                return new PurityAnalysisState { HasPotentialImpurity = mergedImpurity, FirstImpureSyntaxNode = firstImpureNode };
            }

            public bool Equals(PurityAnalysisState other) =>
                this.HasPotentialImpurity == other.HasPotentialImpurity &&
                object.Equals(this.FirstImpureSyntaxNode, other.FirstImpureSyntaxNode); // Compare nodes too

            public override bool Equals(object obj) => obj is PurityAnalysisState other && Equals(other);

            // Fix CS0103: Implement GetHashCode manually for netstandard2.0 compatibility
            public override int GetHashCode()
            {
                // Combine hash codes of both properties
                int hash = 17;
                hash = hash * 23 + HasPotentialImpurity.GetHashCode();
                hash = hash * 23 + (FirstImpureSyntaxNode?.GetHashCode() ?? 0);
                return hash;
            }
            public static bool operator ==(PurityAnalysisState left, PurityAnalysisState right) => left.Equals(right);
            public static bool operator !=(PurityAnalysisState left, PurityAnalysisState right) => !(left == right);
        }

        /// <summary>
        /// Checks if a method symbol is considered pure based on its implementation using CFG data-flow analysis.
        /// Manages the visited set for cycle detection across the entire analysis.
        /// </summary>
        internal static PurityAnalysisResult IsConsideredPure(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol)
        {
            var purityCache = new Dictionary<IMethodSymbol, PurityAnalysisResult>(SymbolEqualityComparer.Default);
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            // Pass the (potentially null) attribute symbol down
            return DeterminePurityRecursiveInternal(methodSymbol.OriginalDefinition, semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol, visited, purityCache);
        }

        /// <summary>
        /// Recursive helper for purity determination. Handles caching and cycle detection.
        /// </summary>
        internal static PurityAnalysisResult DeterminePurityRecursiveInternal(
            IMethodSymbol methodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            LogDebug($"Enter DeterminePurityRecursiveInternal: {methodSymbol.ToDisplayString()}, visited count: {visited.Count}");

            // --- 1. Check Cache --- 
            if (purityCache.TryGetValue(methodSymbol, out var cachedResult))
            {
                LogDebug($"  Purity CACHED: {cachedResult.IsPure} for {methodSymbol.ToDisplayString()}");
                return cachedResult;
            }

            // --- 2. Detect Recursion --- 
            if (!visited.Add(methodSymbol))
            {
                LogDebug($"  Recursion DETECTED for {methodSymbol.ToDisplayString()}. Assuming impure for this path.");
                // Revert to previous behavior: Assume impure on cycle detection
                purityCache[methodSymbol] = PurityAnalysisResult.ImpureUnknownLocation;
                // NOTE: We do NOT remove from visited here, as the original call still needs to complete.
                return PurityAnalysisResult.ImpureUnknownLocation;
            }

            try // Use try/finally to ensure visited.Remove is always called
            {
                // --- 3. Check [Pure] Attribute ---
                var pureAttrSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(PureAttribute).FullName);
                if (pureAttrSymbol != null && HasAttribute(methodSymbol, pureAttrSymbol))
                {
                    LogDebug($"  Method has [Pure] attribute: {methodSymbol.ToDisplayString()}. Returning Pure.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    return PurityAnalysisResult.Pure;
                }

                // --- 4. Known Pure/Impure BCL Members --- 
                if (IsKnownImpure(methodSymbol))
                {
                    LogDebug($"Method {methodSymbol.ToDisplayString()} is known impure.");
                    var knownImpureResult = ImpureResult(null); // Or find syntax if possible
                    purityCache[methodSymbol] = knownImpureResult;
                    return knownImpureResult;
                }
                if (IsKnownPureBCLMember(methodSymbol))
                {
                    LogDebug($"Method {methodSymbol.ToDisplayString()} is known pure BCL member.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    return PurityAnalysisResult.Pure;
                }

                // 1. Abstract/External/Missing Body: Assumed pure (no implementation to violate purity)
                if (methodSymbol.IsAbstract || methodSymbol.IsExtern || GetBodySyntaxNode(methodSymbol, default) == null) // Use default CancellationToken
                {
                    // Only assume pure here if it wasn't already caught by the known lists above
                    LogDebug($"Method {methodSymbol.ToDisplayString()} is abstract, extern, or has no body AND not known impure/pure. Assuming pure.");
                    purityCache[methodSymbol] = PurityAnalysisResult.Pure;
                    return PurityAnalysisResult.Pure;
                }

                // --- Analyze Body using CFG ---
                PurityAnalysisResult result = PurityAnalysisResult.Pure; // Assume pure until proven otherwise by CFG
                var bodySyntaxNode = GetBodySyntaxNode(methodSymbol, default); // Pass CancellationToken.None
                if (bodySyntaxNode != null)
                {
                    LogDebug($"Analyzing body of {methodSymbol.ToDisplayString()} using CFG.");
                    // Call internal CFG analysis helper
                    result = AnalyzePurityUsingCFGInternal(
                        bodySyntaxNode,
                        semanticModel,
                        enforcePureAttributeSymbol,
                        allowSynchronizationAttributeSymbol,
                        visited,
                        methodSymbol, // Pass the containing method symbol
                        purityCache);
                }
                else
                {
                    LogDebug($"No body found for {methodSymbol.ToDisplayString()} to analyze with CFG. Assuming pure based on earlier checks.");
                    // Result remains Pure if no body found (matches abstract/extern check)
                }

                // Get the IOperation for the body *after* potential CFG analysis
                // Used for post-CFG checks (Return, Throw)
                IOperation? methodBodyIOperation = null;
                if (bodySyntaxNode != null)
                {
                    try
                    {
                        methodBodyIOperation = semanticModel.GetOperation(bodySyntaxNode, default);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"  Post-CFG: Error getting IOperation for method body: {ex.Message}");
                        methodBodyIOperation = null; // Ensure it's null if GetOperation fails
                    }
                }

                // --- NEW: Post-CFG Full Operation Tree Walk ---
                // If CFG analysis didn't find impurity, perform a full walk of the
                // IOperation tree as a fallback to catch things missed by CFG structure.
                if (result.IsPure && methodBodyIOperation != null)
                {
                    LogDebug($"  Post-CFG: CFG result is Pure. Performing full IOperation tree walk for {methodSymbol.ToDisplayString()}");
                    // Use the REFINED walker
                    var fullWalker = new FullOperationPurityWalker(semanticModel, enforcePureAttributeSymbol, allowSynchronizationAttributeSymbol, visited, purityCache, methodSymbol);
                    fullWalker.Visit(methodBodyIOperation);

                    if (!fullWalker.OverallPurityResult.IsPure)
                    {
                        LogDebug($"  Post-CFG: IMPURITY FOUND during full IOperation walk. Overriding CFG result.");
                        result = fullWalker.OverallPurityResult;
                    }
                    else
                    {
                        LogDebug($"  Post-CFG: No impurity found during full IOperation walk.");
                    }
                }
                // --- END NEW ---

                // --- Post-CFG Check: Return Values (Original Check) ---
                // If the analysis result is still pure after CFG + Full Walk, explicitly check Return operations
                if (result.IsPure && methodBodyIOperation != null)
                {
                    LogDebug($"Post-CFG: Result Pure. Performing post-CFG check on ReturnOperations in {methodSymbol.ToDisplayString()}");
                    var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

                    var returnContext = new Rules.PurityAnalysisContext(
                        semanticModel,
                        enforcePureAttributeSymbol,
                        pureAttributeSymbol,
                        allowSynchronizationAttributeSymbol,
                        visited,
                        purityCache,
                        methodSymbol,
                        _purityRules,
                        CancellationToken.None);

                    bool returnFound = false;
                    foreach (var returnOp in methodBodyIOperation.DescendantsAndSelf().OfType<IReturnOperation>())
                    {
                        returnFound = true;
                        LogDebug($"  Post-CFG: Found ReturnOperation: {returnOp.Syntax}");
                        if (returnOp.ReturnedValue != null)
                        {
                            LogDebug($"    Post-CFG: Checking ReturnedValue of kind {returnOp.ReturnedValue.Kind}: {returnOp.ReturnedValue.Syntax}");
                            var returnPurity = CheckSingleOperation(returnOp.ReturnedValue, returnContext);
                            if (!returnPurity.IsPure)
                            {
                                LogDebug($"    Post-CFG: Returned value found IMPURE. Overriding result.");
                                result = returnPurity;
                                break; // Found impurity, stop checking returns
                            }
                            else
                            {
                                LogDebug($"    Post-CFG: Returned value checked and found PURE.");
                            }
                        }
                        else
                        {
                            LogDebug($"  Post-CFG: ReturnOperation has no ReturnedValue (e.g., return;). Pure.");
                        }
                    }
                    if (!returnFound)
                    {
                        LogDebug($"  Post-CFG: No ReturnOperation found in method body operation tree.");
                    }
                }
                // --- END Post-CFG Check: Return Values (Original Check) ---

                // --- FIX: Post-CFG Check for Throw Operations ---
                // Even if CFG/Return checks passed, explicitly check for throw statements in the operation tree
                // Use the retrieved methodBodyIOperation
                if (result.IsPure && methodBodyIOperation != null)
                {
                    LogDebug($"Post-CFG: Result still Pure. Performing post-CFG check for ThrowOperations in {methodSymbol.ToDisplayString()}");
                    // Use the retrieved methodBodyIOperation
                    var firstThrowOp = methodBodyIOperation.DescendantsAndSelf().OfType<IThrowOperation>().FirstOrDefault();
                    if (firstThrowOp != null)
                    {
                        LogDebug($"  Post-CFG: Found ThrowOperation: {firstThrowOp.Syntax}. Overriding result to Impure.");
                        result = PurityAnalysisResult.Impure(firstThrowOp.Syntax);
                    }
                    else
                    {
                        LogDebug($"  Post-CFG: No ThrowOperation found in method body operation tree.");
                    }
                }
                // --- END FIX ---

                // --- Caching and Cleanup ---
                purityCache[methodSymbol] = result;

                LogDebug($"Exiting DeterminePurityRecursiveInternal for {methodSymbol.ToDisplayString()}, Final IsPure={result.IsPure}");
                return result;
            }
            finally
            {
                // --- Ensure removal from visited set --- 
                visited.Remove(methodSymbol);
                LogDebug($"Exit DeterminePurityRecursiveInternal: {methodSymbol.ToDisplayString()}");
            }
        }

        /// <summary>
        /// Performs the actual purity analysis using the Control Flow Graph.
        /// </summary>
        private static PurityAnalysisResult AnalyzePurityUsingCFGInternal(
            SyntaxNode bodyNode,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            ControlFlowGraph? cfg = null;
            try
            {
                cfg = ControlFlowGraph.Create(bodyNode, semanticModel);
                LogDebug($"CFG created successfully for node: {bodyNode.Kind()}");
            }
            catch (Exception ex)
            {
                LogDebug($"Error creating ControlFlowGraph for {containingMethodSymbol.ToDisplayString()}: {ex.Message}. Assuming impure.");
                return PurityAnalysisResult.Impure(bodyNode); // If CFG fails, assume impure
            }


            if (cfg == null || cfg.Blocks.IsEmpty)
            {
                LogDebug($"CFG is null or empty for {containingMethodSymbol.ToDisplayString()}. Assuming pure (no operations).");
                return PurityAnalysisResult.Pure; // Empty CFG means no operations, hence pure
            }

            // +++ Log CFG Block Count +++
            LogDebug($"  [CFG] Created CFG with {cfg.Blocks.Length} blocks for {containingMethodSymbol.ToDisplayString()}.");

            // --- Dataflow Analysis Setup ---
            var blockStates = new Dictionary<BasicBlock, PurityAnalysisState>(cfg.Blocks.Length);
            var worklist = new Queue<BasicBlock>();

            // Initialize: Assume all blocks start pure, add entry block to worklist
            LogDebug("  [CFG] Initializing CFG block states to Pure.");
            foreach (var block in cfg.Blocks)
            {
                blockStates[block] = PurityAnalysisState.Pure;
            }
            if (cfg.Blocks.Any()) // Use Any() for safety, though checked IsEmpty above
            {
                var entryBlock = cfg.Blocks.First();
                // +++ Log initial worklist add +++
                LogDebug($"  [CFG] Adding Entry Block #{entryBlock.Ordinal} to worklist.");
                worklist.Enqueue(entryBlock); // Use First() for entry block
            }
            else
            {
                LogDebug("  [CFG] CFG has no blocks. Exiting analysis.");
                return PurityAnalysisResult.Pure; // No blocks = pure
            }

            // --- Dataflow Analysis Loop ---
            LogDebug("  [CFG] Starting CFG dataflow analysis worklist loop.");
            int loopIterations = 0; // Add iteration counter for safety
            // +++ Log right before the loop condition check +++
            LogDebug($"  [CFG] BEFORE WHILE CHECK: worklist.Count = {worklist.Count}, loopIterations = {loopIterations}");
            while (worklist.Count > 0 && loopIterations < cfg.Blocks.Length * 5) // Add loop limit
            {
                // +++ Log immediately inside the loop +++
                LogDebug("  [CFG] ENTERED WHILE LOOP.");
                loopIterations++;
                // +++ Log worklist count and dequeued block +++
                LogDebug($"  [CFG] Worklist count: {worklist.Count}. Iteration: {loopIterations}");
                var currentBlock = worklist.Dequeue();
                LogDebug($"  [CFG] Processing CFG Block #{currentBlock.Ordinal}");

                var stateBefore = blockStates[currentBlock];

                LogDebug($"  [CFG] StateBefore for Block #{currentBlock.Ordinal}: Impure={stateBefore.HasPotentialImpurity}");


                // Apply transfer function to get state after this block's operations
                var stateAfter = ApplyTransferFunction(
                    currentBlock,
                    stateBefore,
                    semanticModel,
                    enforcePureAttributeSymbol,
                    allowSynchronizationAttributeSymbol,
                    visited,
                    containingMethodSymbol,
                    purityCache);


                LogDebug($"  [CFG] State after Block #{currentBlock.Ordinal}: Impure={stateAfter.HasPotentialImpurity}");


                // --- FIX: Always propagate the calculated stateAfter to successors ---
                // The PropagateToSuccessor method will handle whether the successor needs enqueuing.
                LogDebug($"  [CFG] Propagating stateAfter (Impure={stateAfter.HasPotentialImpurity}) to successors of Block #{currentBlock.Ordinal}.");
                PropagateToSuccessor(currentBlock.ConditionalSuccessor?.Destination, stateAfter, blockStates, worklist);
                PropagateToSuccessor(currentBlock.FallThroughSuccessor?.Destination, stateAfter, blockStates, worklist);
                // --- END FIX ---
            }
            // +++ Log loop termination reason +++
            if (worklist.Count == 0)
            {
                LogDebug("  [CFG] Finished CFG dataflow analysis worklist loop (worklist empty).");
            }
            else
            {
                LogDebug($"  [CFG] WARNING: Exited CFG dataflow loop due to iteration limit ({loopIterations}). Potential infinite loop?");
            }


            // --- Aggregate Result ---
            PurityAnalysisResult finalResult;
            BasicBlock? exitBlock = cfg.Blocks.LastOrDefault(b => b.Kind == BasicBlockKind.Exit); // Ensure we get the actual Exit block

            if (exitBlock != null && blockStates.TryGetValue(exitBlock, out var exitState))
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Exit Block #{exitBlock.Ordinal} Final State: HasImpurity={exitState.HasPotentialImpurity}, Node={exitState.FirstImpureSyntaxNode?.Kind()}, NodeText='{exitState.FirstImpureSyntaxNode?.ToString()}'");

                // --- FIX: Explicitly check operations in the exit block if state is currently pure ---
                if (!exitState.HasPotentialImpurity)
                {
                    LogDebug($"  [CFG] Exit block state is pure. Explicitly checking operations within Exit Block #{exitBlock.Ordinal}.");
                    var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

                    var ruleContext = new PurelySharp.Analyzer.Engine.Rules.PurityAnalysisContext(
                        semanticModel,
                        enforcePureAttributeSymbol,
                        pureAttributeSymbol,
                        allowSynchronizationAttributeSymbol,
                        visited, // Note: visited might be incomplete here, but ok for stateless rules
                        purityCache,
                        containingMethodSymbol,
                        _purityRules,
                        CancellationToken.None); // Pass the token

                    foreach (var exitOp in exitBlock.Operations)
                    {
                        if (exitOp == null) continue;
                        LogDebug($"    [CFG] Checking exit operation: {exitOp.Kind} - '{exitOp.Syntax}'");
                        var opResult = CheckSingleOperation(exitOp, ruleContext);
                        if (!opResult.IsPure)
                        {
                            LogDebug($"    [CFG] Exit operation {exitOp.Kind} found IMPURE. Updating final result.");
                            exitState = new PurityAnalysisState { HasPotentialImpurity = true, FirstImpureSyntaxNode = opResult.ImpureSyntaxNode ?? exitOp.Syntax };
                            // Update exitState for the final result calculation below
                            break; // Found impurity, no need to check other exit operations
                        }
                    }
                    if (!exitState.HasPotentialImpurity)
                    {
                        LogDebug($"  [CFG] All exit block operations checked and found pure.");
                    }
                }
                // --- END FIX ---

                // Use the potentially updated exitState to determine the final result
                finalResult = exitState.HasPotentialImpurity
                    ? PurityAnalysisResult.Impure(exitState.FirstImpureSyntaxNode ?? bodyNode)
                    : PurityAnalysisResult.Pure;
            }
            else if (exitBlock != null) // Has exit block, but state not found?
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: Could not get state for the exit block #{exitBlock.Ordinal}. Assuming impure (e.g., unreachable code).");
                finalResult = PurityAnalysisResult.Impure(bodyNode);
            }
            else // No blocks in CFG
            {
                LogDebug($"  [CFG] CFG Result Aggregation for {containingMethodSymbol.ToDisplayString()}: CFG has no blocks. Assuming pure.");
                finalResult = PurityAnalysisResult.Pure; // Should have been caught earlier
            }

            return finalResult;
        }

        /// <summary>
        /// Applies the transfer function for a single basic block in the CFG.
        /// Determines the purity state after executing the operations in the block.
        /// </summary>
        private static PurityAnalysisState ApplyTransferFunction(
            BasicBlock block,
            PurityAnalysisState stateBefore,
            SemanticModel semanticModel,
            INamedTypeSymbol enforcePureAttributeSymbol,
            INamedTypeSymbol? allowSynchronizationAttributeSymbol,
            HashSet<IMethodSymbol> visited,
            IMethodSymbol containingMethodSymbol,
            Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache)
        {
            LogDebug($"ApplyTransferFunction START for Block #{block.Ordinal} - Initial State: Impure={stateBefore.HasPotentialImpurity}");

            // +++ Log ALL raw operations in the block upon entry +++
            LogDebug($"    [ATF Raw Ops - Block {block.Ordinal}] START");
            foreach (var rawOp in block.Operations)
            {
                if (rawOp != null)
                {
                    LogDebug($"      - Raw Kind: {rawOp.Kind}, Raw Syntax: {rawOp.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");
                }
                else
                {
                    LogDebug("      - Raw Null Operation");
                }
            }
            LogDebug($"    [ATF Raw Ops - Block {block.Ordinal}] END");
            // +++ End Raw Log +++

            if (stateBefore.HasPotentialImpurity) // Optimization: If already impure, no need to check further.
            {
                LogDebug($"ApplyTransferFunction SKIP for Block #{block.Ordinal} - Already impure.");
                return stateBefore;
            }

            // Create context ONCE for this block's analysis
            var pureAttributeSymbol_block = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");

            var ruleContext = new PurelySharp.Analyzer.Engine.Rules.PurityAnalysisContext(
                semanticModel,
                enforcePureAttributeSymbol,
                pureAttributeSymbol_block,
                allowSynchronizationAttributeSymbol,
                visited,
                purityCache,
                containingMethodSymbol,
                _purityRules, // Pass the list of rules
                CancellationToken.None); // Pass the token

            // +++ Log ALL operations in the block +++
            LogDebug($"    [ATF Block {block.Ordinal}] Operations:");
            foreach (var op in block.Operations)
            {
                if (op != null)
                {
                    LogDebug($"      - Kind: {op.Kind}, Syntax: {op.Syntax.ToString().Replace("\r\n", " ").Replace("\n", " ")}");
                }
                else
                {
                    LogDebug("      - Null Operation");
                }
            }
            LogDebug($"    [ATF Block {block.Ordinal}] End Operations Log.");
            // +++ End Log +++

            // If we reach here, all operations in the block were handled by rules and deemed pure.
            LogDebug($"ApplyTransferFunction END for Block #{block.Ordinal} - All ops handled and pure. Returning previous state.");
            return stateBefore; // Return the initial (Pure) state if no operations caused impurity
        }

        /// <summary>
        /// Checks the purity of a single IOperation using the registered purity rules.
        /// </summary>
        internal static PurityAnalysisResult CheckSingleOperation(IOperation operation, Rules.PurityAnalysisContext context)
        {
            LogDebug($"    [CSO] Enter CheckSingleOperation for Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");

            // Explicitly handle FlowCaptureReference and FlowCapture as pure.
            // These represent compiler-generated temporaries and should not affect purity.
            if (operation.Kind == OperationKind.FlowCaptureReference || operation.Kind == OperationKind.FlowCapture)
            {
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure - FlowCapture/Reference)");
                return PurityAnalysisResult.Pure;
            }


            // Find the first applicable rule
            var applicableRule = _purityRules.FirstOrDefault(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

            if (applicableRule != null)
            {
                // +++ Log Rule Application +++
                LogDebug($"    [CSO] Applying Rule '{applicableRule.GetType().Name}' to Kind: {operation.Kind}, Syntax: '{operation.Syntax.ToString().Trim()}'");
                var ruleResult = applicableRule.CheckPurity(operation, context);
                // +++ Log Rule Result +++
                LogDebug($"    [CSO] Rule '{applicableRule.GetType().Name}' Result: IsPure={ruleResult.IsPure}");
                if (!ruleResult.IsPure)
                {
                    LogDebug($"    [CSO] Exit CheckSingleOperation (Impure from rule)");
                    return ruleResult; // Return the impure result
                }
                // Rule handled it and found it pure, stop checking this op
                LogDebug($"    [CSO] Exit CheckSingleOperation (Pure from rule)");
                return PurityAnalysisResult.Pure;
            }
            else
            {
                // Default assumption: If no rule handles it, assume impure for safety.
                LogDebug($"    [CSO] No rule found for operation kind {operation.Kind}. Defaulting to impure. Syntax: '{operation.Syntax.ToString().Trim()}'");
                LogDebug($"    [CSO] Exit CheckSingleOperation (Impure default)");
                return ImpureResult(operation.Syntax); // Restore OLD BEHAVIOR
            }
        }

        // ========================================================================
        // Helper Methods (made internal or added)
        // ========================================================================

        /// <summary>
        /// Checks if a symbol (method, property) corresponds to a known BCL member considered pure.
        /// </summary>
        internal static bool IsKnownPureBCLMember(ISymbol symbol)
        {
            if (symbol == null) return false;

            // 1. Check specific immutable collection methods/properties by name/type
            if (symbol.ContainingType?.ContainingNamespace?.ToString().StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true)
            {
                // Assume most operations on immutable types are pure (reading properties, common methods)
                // Be slightly more specific for factory methods
                if (symbol.Name.Contains("Create") || symbol.Name.Contains("Add") || symbol.Name.Contains("Set") || symbol.Name.Contains("Remove"))
                {
                    // Factory/mutation methods on the *static* class (like ImmutableList.Create) are pure.
                    // Instance methods like Add/SetItem return *new* collections and are pure reads of the original.
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable member: {symbol.ToDisplayString()}");
                    return true;
                }
                // Check common read properties
                if (symbol.Kind == SymbolKind.Property && (symbol.Name == "Count" || symbol.Name == "Length" || symbol.Name == "IsEmpty"))
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable property: {symbol.ToDisplayString()}");
                    return true;
                }
                // Check common read methods
                if (symbol.Kind == SymbolKind.Method && (symbol.Name == "Contains" || symbol.Name == "IndexOf" || symbol.Name == "TryGetValue"))
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Collections.Immutable method: {symbol.ToDisplayString()}");
                    return true;
                }
            }

            // 2. Check against the known pure list using the original definition's display string
            string signature = symbol.OriginalDefinition.ToDisplayString();

            // *** FIX: Append .get for Property Symbols before HashSet check ***
            if (symbol.Kind == SymbolKind.Property)
            {
                // We assume checks in this helper are for *reading* the property.
                // Append ".get" to match the HashSet entries for property getters.
                if (!signature.EndsWith(".get") && !signature.EndsWith(".set")) // Avoid double appending
                {
                    signature += ".get";
                    PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Appended .get to property signature: \"{signature}\"");
                }
            }

            // +++ Add detailed logging before Contains check +++
            PurityAnalysisEngine.LogDebug($"    [IsKnownPure] Checking HashSet.Contains for signature: \"{signature}\"");
            bool isKnownPure = KnownPureBCLMembers.Contains(signature);
            // +++ Log the result of Contains +++
            PurityAnalysisEngine.LogDebug($"    [IsKnownPure] HashSet.Contains result: {isKnownPure}");

            // Handle common generic cases (e.g., List<T>.Count) more robustly if direct match fails
            if (!isKnownPure && symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                isKnownPure = KnownPureBCLMembers.Contains(signature);
            }
            else if (!isKnownPure && symbol is IPropertySymbol propertySymbol && propertySymbol.ContainingType.IsGenericType)
            {
                // Check property on constructed generic type vs definition
                // Example: "System.Collections.Generic.List<T>.Count.get"
                // Special handling for indexers
                if (propertySymbol.IsIndexer)
                {
                    // Construct signature like "Namespace.Type<T>.this[params].get"
                    // Note: Getting exact parameter types for signature matching can be complex.
                    // For now, rely on the OriginalDefinition check first, which might handle it.
                    // If OriginalDefinition check fails, this specific generic check might still fail for indexers
                    // without more precise parameter type matching.
                    // Let's try matching the original definition string first for indexers.
                    signature = propertySymbol.OriginalDefinition.ToDisplayString(); // Use original definition string

                }
                else
                {
                    signature = $"{propertySymbol.ContainingType.ConstructedFrom.ToDisplayString()}.{propertySymbol.Name}.get"; // Assuming 'get' suffix
                }
                isKnownPure = KnownPureBCLMembers.Contains(signature);
            }


            if (isKnownPure)
            {
                LogDebug($"Helper IsKnownPureBCLMember: Match found for {symbol.ToDisplayString()} using signature '{signature}'");
            }
            else
            {
                // Fallback: Check if it's in System.Math as most Math methods are pure
                // This is a broad check; KnownPureBCLMembers is preferred for specifics
                if (symbol.ContainingNamespace?.ToString().Equals("System", StringComparison.Ordinal) == true &&
                    symbol.ContainingType?.Name.Equals("Math", StringComparison.Ordinal) == true)
                {
                    LogDebug($"Helper IsKnownPureBCLMember: Assuming pure for System.Math member: {symbol.ToDisplayString()}");
                    isKnownPure = true; // Treat all System.Math as pure for now
                }
            }

            return isKnownPure;
        }

        /// <summary>
        /// Checks if a symbol (method, property) corresponds to a known member considered IMPURE.
        /// </summary>
        internal static bool IsKnownImpure(ISymbol symbol)
        {
            if (symbol == null) return false;
            // Check method/property signature against known impure list
            string signature = symbol.OriginalDefinition.ToDisplayString();

            // *** FIX: Append .get for Property Symbols before HashSet check ***
            if (symbol.Kind == SymbolKind.Property)
            {
                // We assume checks in this helper are for *reading* the property.
                // Append ".get" to match the HashSet entries for property getters.
                if (!signature.EndsWith(".get") && !signature.EndsWith(".set")) // Avoid double appending
                {
                    signature += ".get";
                    PurityAnalysisEngine.LogDebug($"    [IsKnownImpure] Appended .get to property signature: \"{signature}\"");
                }
            }

            if (KnownImpureMethods.Contains(signature))
            {
                LogDebug($"Helper IsKnownImpure: Match found for {symbol.ToDisplayString()} using signature '{signature}'");
                return true;
            }

            // Handle generic methods if needed (e.g., Interlocked.CompareExchange<T>)
            if (symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                signature = methodSymbol.ConstructedFrom.ToDisplayString();
                if (KnownImpureMethods.Contains(signature))
                {
                    LogDebug($"Helper IsKnownImpure: Generic match found for {symbol.ToDisplayString()} using signature '{signature}'");
                    return true;
                }
            }

            // Additional check: Property access on known impure types (e.g., DateTime.Now)
            if (symbol is IPropertySymbol property && IsInImpureNamespaceOrType(property.ContainingType)) // Check containing type too
            {
                // We might have specific properties listed in KnownImpureMethods (like DateTime.Now.get)
                // This is a fallback if the type itself is generally impure.
                LogDebug($"Helper IsKnownImpure: Property access {symbol.ToDisplayString()} on known impure type {property.ContainingType.ToDisplayString()}.");
                // return true; // Be careful: A type might have *some* pure properties. Rely on KnownImpureMethods first.
            }

            // Check if the method is an Interlocked operation (often requires special handling)
            if (symbol.ContainingType?.ToString().Equals("System.Threading.Interlocked", StringComparison.Ordinal) ?? false)
            {
                LogDebug($"Helper IsKnownImpure: Member {symbol.ToDisplayString()} belongs to System.Threading.Interlocked.");
                return true; // All Interlocked methods are treated as impure
            }

            return false;
        }


        /// <summary>
        /// Checks if the symbol belongs to a namespace or type known to be generally impure.
        /// </summary>
        internal static bool IsInImpureNamespaceOrType(ISymbol symbol)
        {
            if (symbol == null) return false;

            PurityAnalysisEngine.LogDebug($"    [INOT] Checking symbol: {symbol.ToDisplayString()}");

            // Check the containing type first
            INamedTypeSymbol? containingType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            while (containingType != null)
            {
                // *** Key Check 1: Type Name ***
                string typeName = containingType.OriginalDefinition.ToDisplayString(); // Get the fully qualified name
                PurityAnalysisEngine.LogDebug($"    [INOT] Checking type: {typeName}"); // Log the exact string
                PurityAnalysisEngine.LogDebug($"    [INOT] Comparing '{typeName}' against KnownImpureTypeNames..."); // Log before comparison
                if (KnownImpureTypeNames.Contains(typeName)) // Compare against the known impure type list
                {
                    PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure type: {typeName}");
                    return true;
                }

                // Check containing namespace of the type
                INamespaceSymbol? ns = containingType.ContainingNamespace;
                while (ns != null && !ns.IsGlobalNamespace)
                {
                    string namespaceName = ns.ToDisplayString();
                    PurityAnalysisEngine.LogDebug($"    [INOT] Checking namespace: {namespaceName}");
                    if (KnownImpureNamespaces.Contains(namespaceName))
                    {
                        PurityAnalysisEngine.LogDebug($"    [INOT] --> Match found for impure namespace: {namespaceName}");
                        return true;
                    }
                    ns = ns.ContainingNamespace;
                }

                PurityAnalysisEngine.LogDebug($"    [INOT] Checking containing type of {containingType.Name}");
                containingType = containingType.ContainingType; // Check nested types
            }

            PurityAnalysisEngine.LogDebug($"    [INOT] No impure type or namespace match found for: {symbol.ToDisplayString()}");
            return false;
        }


        /// <summary>
        /// Checks if a symbol is marked with the [EnforcePure] attribute.
        /// </summary>
        internal static bool IsPureEnforced(ISymbol symbol, INamedTypeSymbol enforcePureAttributeSymbol)
        {
            if (symbol == null || enforcePureAttributeSymbol == null)
            {
                return false;
            }
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, enforcePureAttributeSymbol));
        }

        /// <summary>
        /// Helper to create an impure result, using the unknown location if the syntax node is null.
        /// </summary>
        internal static PurityAnalysisResult ImpureResult(SyntaxNode? syntaxNode)
        {
            return syntaxNode != null ? PurityAnalysisResult.Impure(syntaxNode) : PurityAnalysisResult.ImpureUnknownLocation;
        }

        /// <summary>
        /// Logs debug messages (conditionally based on build configuration or settings).
        /// Made internal for access by rules.
        /// </summary>
        internal static void LogDebug(string message)
        {
#if DEBUG
            // New logging implementation: Write to Console
            /* // Commented out to disable logging
            try
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [DEBUG] {message}");
            }
            catch (Exception ex)
            {
                // Fallback if Console logging fails for some reason
                System.Diagnostics.Debug.WriteLine($"Console Logging failed: {ex.Message}");
            }
            */
#endif
        }

        /// <summary>
        /// Gets the syntax node representing the body of a method symbol.
        /// </summary>
        private static SyntaxNode? GetBodySyntaxNode(IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            // Try to get MethodDeclarationSyntax or LocalFunctionStatementSyntax body
            var declaringSyntaxes = methodSymbol.DeclaringSyntaxReferences;
            foreach (var syntaxRef in declaringSyntaxes)
            {
                var syntaxNode = syntaxRef.GetSyntax(cancellationToken); // Use cancellation token

                // Return the declaration node itself, ControlFlowGraph.Create can handle these.
                if (syntaxNode is MethodDeclarationSyntax ||
                    syntaxNode is LocalFunctionStatementSyntax ||
                    syntaxNode is AccessorDeclarationSyntax ||
                    syntaxNode is ConstructorDeclarationSyntax ||
                    syntaxNode is OperatorDeclarationSyntax) // Added Operator
                {
                    return syntaxNode;
                }

                // For properties with expression bodies, maybe return the ArrowExpressionClauseSyntax?
                // Let's stick to returning the main declaration syntax for now.
            }
            return null;
        }

        // --- Re-added PropagateToSuccessor --- Needs MergeStates
        private static void PropagateToSuccessor(BasicBlock? successor, PurityAnalysisState newState, Dictionary<BasicBlock, PurityAnalysisState> blockStates, Queue<BasicBlock> worklist)
        {
            if (successor == null) return;

            // +++ Check if successor state exists (indicates prior visit from propagation) +++
            bool previouslyVisited = blockStates.TryGetValue(successor, out var existingState);
            // If not previously visited, existingState defaults to 'Pure' (struct default)

            var mergedState = MergeStates(existingState, newState);

            // +++ Determine if state changed or if it's the first propagation visit +++
            bool stateChanged = !mergedState.Equals(existingState);
            // We determine first visit based on whether the key existed in blockStates before the merge.
            // Note: This assumes initialization didn't prepopulate blockStates.
            // If blockStates IS prepopulated (e.g., with PurityAnalysisState.Pure), this 'firstVisit' logic won't work.
            // Let's assume initialization leaves blockStates empty or doesn't include all blocks initially.
            // RETHINK: Our current init DOES prepopulate. So 'previouslyVisited' indicates if *any* propagation reached it.
            // We need a different way to track first processing via worklist, or change init.

            // --- Simpler Logic --- 
            // Always update the state. Enqueue if state changed OR if it's not in the worklist yet.
            // This ensures first visit gets enqueued, and subsequent changes also trigger re-enqueue.

            if (stateChanged)
            {
                LogDebug($"PropagateToSuccessor: State changed for Block #{successor.Ordinal} from Impure={existingState.HasPotentialImpurity} to Impure={mergedState.HasPotentialImpurity}. Updating state.");
                blockStates[successor] = mergedState;
            }
            else
            {
                // If state didn't change, but it was never added to blockStates before, update it now.
                if (!previouslyVisited)
                {
                    blockStates[successor] = mergedState;
                }
                // Log regardless if state changed or not
                LogDebug($"PropagateToSuccessor: State unchanged for Block #{successor.Ordinal} (Impure={existingState.HasPotentialImpurity}).");
            }

            // Enqueue if state changed OR if it's not already in the queue 
            // This ensures initial propagation and reprocessing on change.
            if (stateChanged || !worklist.Contains(successor)) // Check Contains *before* potentially adding
            {
                if (!worklist.Contains(successor))
                {
                    LogDebug($"PropagateToSuccessor: Enqueuing Block #{successor.Ordinal} (State Changed: {stateChanged}).");
                    worklist.Enqueue(successor);
                }
                else
                {
                    // Already in queue. If state changed, it will be reprocessed with new state.
                    // If state didn't change, no need to re-enqueue.
                    if (stateChanged)
                    {
                        LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue, state changed. Will reprocess.");
                    }
                    else
                    {
                        LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue, state unchanged.");
                    }
                }
            }
            else
            {
                LogDebug($"PropagateToSuccessor: Block #{successor.Ordinal} already in queue and state unchanged. No enqueue needed.");
            }
        }

        // --- Added MergeStates helper --- (Needed by PropagateToSuccessor)
        private static PurityAnalysisState MergeStates(PurityAnalysisState state1, PurityAnalysisState state2)
        {
            // If either state is impure, the merged state is impure.
            if (state1.HasPotentialImpurity || state2.HasPotentialImpurity)
            {
                // Try to keep the first impure node encountered.
                // This isn't perfect without path tracking, but choose the one that isn't null.
                SyntaxNode? firstImpureNode = null;
                if (state1.HasPotentialImpurity && state2.HasPotentialImpurity)
                {
                    // Both impure, prefer the existing one? Or the new one?
                    // Let's prefer the one that isn't null. If both are not null, pick state1's arbitrarily.
                    firstImpureNode = state1.FirstImpureSyntaxNode ?? state2.FirstImpureSyntaxNode;
                }
                else if (state1.HasPotentialImpurity)
                {
                    firstImpureNode = state1.FirstImpureSyntaxNode;
                }
                else // Only state2 is impure
                {
                    firstImpureNode = state2.FirstImpureSyntaxNode;
                }

                return new PurityAnalysisState { HasPotentialImpurity = true, FirstImpureSyntaxNode = firstImpureNode };
            }

            // Both are pure
            return PurityAnalysisState.Pure;
        }

        // +++ ADDED HasAttribute HELPER +++
        internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        {
            if (attributeSymbol == null) return false; // Guard against null attribute symbol
            return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass?.OriginalDefinition, attributeSymbol.OriginalDefinition));
        }
        // --- END ADDED HELPER ---

        // --- NEW: REFINED FullOperationPurityWalker Helper Class ---
        private class FullOperationPurityWalker : OperationWalker
        {
            private readonly Rules.PurityAnalysisContext _context;
            private PurityAnalysisResult _overallPurityResult = PurityAnalysisResult.Pure;
            private bool _firstImpurityFound = false;

            public FullOperationPurityWalker(
                SemanticModel semanticModel,
                INamedTypeSymbol enforcePureAttributeSymbol,
                INamedTypeSymbol? allowSynchronizationAttributeSymbol,
                HashSet<IMethodSymbol> visited,
                Dictionary<IMethodSymbol, PurityAnalysisResult> purityCache,
                IMethodSymbol containingMethodSymbol)
            {
                var pureAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName("PurelySharp.Attributes.PureAttribute");
                _context = new Rules.PurityAnalysisContext(
                         semanticModel,
                         enforcePureAttributeSymbol,
                         pureAttributeSymbol,
                         allowSynchronizationAttributeSymbol,
                         visited,
                         purityCache,
                         containingMethodSymbol,
                         _purityRules,
                         CancellationToken.None);
            }

            public PurityAnalysisResult OverallPurityResult => _overallPurityResult;

            public override void VisitWith(IWithOperation operation)
            {
                if (_firstImpurityFound) return; // Stop walking if impurity already found

                LogDebug($"    [Final Walker] Visiting: With - '{operation.Syntax}'");
                // Explicitly check the 'with' operation itself using rules
                var withResult = CheckSingleOperation(operation, _context); // Calls WithOperationPurityRule
                if (!withResult.IsPure)
                {
                    LogDebug($"    [Final Walker] IMPURITY FOUND by CheckSingleOperation: With at '{operation.Syntax}'");
                    _overallPurityResult = withResult; // Use the result from the rule
                    _firstImpurityFound = true;
                    // Don't visit children if the operation itself is impure
                    return;
                }
                else
                {
                    // If the rule handled it and found it pure, we assume the rule correctly
                    // analyzed the necessary children (Operand, Initializer Values).
                    // Therefore, DO NOT call base.VisitWith(operation) which would descend further.
                    LogDebug($"    [Final Walker] Kind With checked pure by rule. SKIPPING base.VisitWith.");
                    // base.VisitWith(operation); // <-- DO NOT DESCEND FURTHER
                }
            }

            public override void DefaultVisit(IOperation operation)
            {
                if (_firstImpurityFound) return; // Stop walking if impurity already found

                // Log the kind being visited
                LogDebug($"    [Final Walker] Visiting: {operation.Kind} - '{operation.Syntax}'");

                // Check if the operation itself requires a direct purity check via rules
                bool requiresDirectCheck = _purityRules.Any(rule => rule.ApplicableOperationKinds.Contains(operation.Kind));

                if (requiresDirectCheck)
                {
                    LogDebug($"    [Final Walker] Kind {operation.Kind} needs check via CheckSingleOperation.");
                    var result = CheckSingleOperation(operation, _context);
                    if (!result.IsPure)
                    {
                        LogDebug($"    [Final Walker] IMPURITY FOUND by CheckSingleOperation: {operation.Kind} at '{operation.Syntax}'");
                        _overallPurityResult = result;
                        _firstImpurityFound = true;
                        return; // Stop walking this branch
                    }
                    else
                    {
                        LogDebug($"    [Final Walker] Kind {operation.Kind} checked pure. Visiting children.");
                        // If pure, continue visiting children
                        base.DefaultVisit(operation);
                    }
                }
                else
                {
                    // If no specific rule applies, assume structurally pure FOR THE WALK
                    // and visit children.
                    LogDebug($"    [Final Walker] Kind {operation.Kind} is structurally pure. Visiting children.");
                    base.DefaultVisit(operation);
                }
            }
        }
        // --- END NEW ---
    }
}