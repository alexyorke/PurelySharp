using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine;

public static class Constants
{

    public static readonly ImmutableHashSet<string> KnownImpureNamespaces = ImmutableHashSet.Create(
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

    public static readonly ImmutableHashSet<string> KnownImpureTypeNames = ImmutableHashSet.Create(
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

    public static readonly HashSet<string> KnownImpureMethods = new HashSet<string>(StringComparer.Ordinal)
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
        // --- Re-add JsonSerializer.Deserialize to impure list ---
        "System.Text.Json.JsonSerializer.Deserialize", // ADDED Simplified Entry
        "System.Text.Json.JsonSerializer.Deserialize<TValue>(string, System.Text.Json.JsonSerializerOptions?)",
        "System.Text.Json.JsonSerializer.Deserialize<TValue>(System.ReadOnlySpan<byte>, System.Text.Json.JsonSerializerOptions?)",
        // --- End Re-add ---
        "System.Text.Json.JsonSerializer.DeserializeAsync", // All overloads
        "System.Text.Json.JsonSerializer.SerializeAsync", // All overloads
        "System.Text.StringBuilder.Append(string?)", // Simplified, common overloads - ADDED ?
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

        // --- ADDED: string.Format considered impure due to potential ToString/IFormatProvider side effects ---
        // "string.Format(string, object?)", // Common overload // REMOVED
        // "string.Format(string, object?, object?)", // Common overload // REMOVED
        // "string.Format(string, object?, object?, object?)", // Common overload // REMOVED
        // "string.Format(string, params object?[])", // Param array overload // REMOVED
        // "string.Format(System.IFormatProvider?, string, params object?[])", // Provider overload // REMOVED
        // -----------------------------------------------------------------------------------------------------

        // --- ADDED: string.Split and string.Join --- 
        // "string.Split", // All overloads (allocates array/list) // REMOVED
        // "String.Split", // Added uppercase version // REMOVED
        // "string.Join", // All overloads (iterates, allocates string) // REMOVED
        // -----------------------------------------

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
        // --- REMOVE Pure SortedDictionary methods from Impure list ---
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.Values.get", // Added common property
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.Keys.get", // Added common property
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.Count.get", // Added common property
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsKey(TKey)",
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsValue(TValue)",
        // "System.Collections.Generic.SortedDictionary<TKey, TValue>.TryGetValue(TKey, out TValue)",
        // --- END REMOVE ---
        "System.Collections.Generic.KeyedCollection<TKey, TItem>.Remove(TKey)", // Corrected generic params
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

        // --- System.Xml.Linq --- 
        "System.Xml.Linq.XContainer.Add(object)", // Added - Modifies the container
        "System.Xml.Linq.XElement.Add(object)", // Added - Explicitly add XElement.Add too

        // --- ValueTask/Task related --- 
        "System.Threading.Tasks.ValueTask<TResult>.ValueTask(TResult)", // Add constructor as impure based on test expectations
        "System.Threading.Tasks.Task.Run(System.Action)", // Task.Run should be impure
        "System.IO.MemoryStream.MemoryStream()", // Corrected Signature: Treat constructor as impure by default
        // --- ADDED Sync Serialize/Deserialize --- 
        "System.Text.Json.JsonSerializer.Deserialize", // All overloads (sync)
        "System.Text.Json.JsonSerializer.Serialize", // All overloads (sync)
        // --- END ADD --- 
        "System.Text.Json.JsonSerializer.DeserializeAsync", // All overloads
        "System.Text.Json.JsonSerializer.SerializeAsync", // All overloads
        "System.Text.StringBuilder.Append(string?)", // Simplified, common overloads - ADDED ?
        "System.Security.Cryptography.RandomNumberGenerator.Fill(byte[])",
        // ADDED String.Split
        // "System.String.Split(params char[])", // Allocates array // REMOVED
        // StringBuilder
        "System.Text.StringBuilder.Append(string?)", // Corrected - Remove duplicate


        // --- Added String Split Overloads ---
        // "string.Split(params char[])", // REMOVED
        // "System.String.Split(params char[])", // With full namespace // REMOVED
        // "string.Split(char[])", // Without params keyword // REMOVED

        // "string.Split(char[], int)", // REMOVED
        // "System.String.Split(char[], int)", // REMOVED

        // "string.Split(char[], System.StringSplitOptions)", // REMOVED
        // "System.String.Split(char[], System.StringSplitOptions)", // REMOVED

        // "string.Split(char[], int, System.StringSplitOptions)", // REMOVED
        // "System.String.Split(char[], int, System.StringSplitOptions)", // REMOVED

        // "string.Split(string[], System.StringSplitOptions)", // REMOVED
        // "System.String.Split(string[], System.StringSplitOptions)", // REMOVED

        // "string.Split(string[], int, System.StringSplitOptions)", // REMOVED
        // "System.String.Split(string[], int, System.StringSplitOptions)", // REMOVED
        // --- End Added String Split Overloads ---

        // --- Potentially pure (check context) from list ---
        "System.Convert.ChangeType(object, System.Type)", // Depends on conversion
    };

    public static readonly HashSet<string> KnownPureBCLMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        // --- Pure / Mostly Pure / Conditionally Pure from list ---
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
        // --- ADDED Common Pure LINQ Methods ---
        // "System.Linq.Enumerable.ToList<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // REMOVED
        // "System.Linq.Enumerable.ToArray<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // REMOVED
        // "System.Linq.Enumerable.ToDictionary<TSource, TKey>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TKey>)", // REMOVED
        // "System.Linq.Enumerable.ToHashSet<TSource>(System.Collections.Generic.IEnumerable<TSource>)", // REMOVED
        "System.Linq.Enumerable.Contains<TSource>(System.Collections.Generic.IEnumerable<TSource>, TSource)",
        // --------------------------------------

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

        // --- ADDED: String constructor from Span --- 
        "string.String(System.ReadOnlySpan<char>)",
        // -----------------------------------------

        // System.StringComparer
        "System.StringComparer.InvariantCultureIgnoreCase.Compare(string, string)",
        "System.StringComparer.Ordinal.Equals(string, string)",

        // System.Text
        "System.Text.Encoding.GetBytes(string)",
        "System.Text.Encoding.GetEncoding(string)",
        "System.Text.Encoding.GetString(byte[])",
        "System.Text.Encoding.UTF8.get",
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
        // "System.Text.StringBuilder()", // Constructor - Removed, mutable state handled by usage
        // "System.Text.StringBuilder.Capacity.get", // Removed, reading capacity is pure
        // "System.Text.StringBuilder.ToString()", // Removed, should be pure

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
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsKey(TKey)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsValue(TValue)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.TryGetValue(TKey, out TValue)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Count.get",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Keys.get",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Values.get",
        "System.Collections.Generic.KeyedCollection<TKey, TItem>.Contains(TKey)", // Corrected generic params
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
        "System.Convert.FromBase64String(string)",
        "System.Convert.ToBase64String(byte[])",
        "System.Convert.ToDouble(object)", // Simplified
        "System.Convert.ToInt32(object)", // Simplified
        "System.Convert.ToString(object)", // Simplified
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
        "System.Linq.Enumerable.ToHashSet<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Collections.Generic.List<T>.Find(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.get_Count()",
        "System.String.StartsWith(System.String)",

        // Common pure methods from System.Math
        "System.Math.Abs",

        // --- string.Format --- Added as potentially impure due to ToString overrides / IFormatProvider
        // "string.Format(string, object?)", // Common overload // REMOVED
        // "string.Format(string, object?, object?)", // Common overload // REMOVED
        // "string.Format(string, object?, object?, object?)", // Common overload // REMOVED
        // "string.Format(string, params object?[])", // Param array overload // REMOVED
        // "string.Format(System.IFormatProvider?, string, params object?[])", // Provider overload // REMOVED

        // --- Added String Split Overloads ---
        // "string.Split(params char[])", // REMOVED
        // "string.Split(char[])", // Without params keyword // REMOVED

        // "string.Split(char[], int)", // REMOVED
        // "System.String.Split(char[], int)", // REMOVED

        // "string.Split(char[], System.StringSplitOptions)", // REMOVED
        // "System.String.Split(char[], System.StringSplitOptions)", // REMOVED

        // "string.Split(char[], int, System.StringSplitOptions)", // REMOVED
        // "System.String.Split(char[], int, System.StringSplitOptions)", // REMOVED

        // "string.Split(string[], System.StringSplitOptions)", // REMOVED
        // "System.String.Split(string[], System.StringSplitOptions)", // REMOVED

        // "string.Split(string[], int, System.StringSplitOptions)", // REMOVED
        // "System.String.Split(string[], int, System.StringSplitOptions)", // REMOVED
        // --- End Added String Split Overloads ---

        // --- Potentially pure (check context) from list ---
        // "System.Convert.ChangeType(object, System.Type)", // Depends on conversion // REMOVED

        // --- ADDED: System.Object ---
        "object.Equals(object)", // Often pure, though overrides can be impure
        "object.GetHashCode()", // Often pure if immutable or based on immutable fields
        "object.GetType()", // Pure
        "object.ReferenceEquals(object, object)", // Pure
        "object.ToString()", // Depends on override, but often pure for value types/immutables

        // System.ReadOnlySpan<T> / Span<T> (Properties/Methods often pure reads)
        "System.ReadOnlySpan<T>.Length.get",
        "System.ReadOnlySpan<T>.IsEmpty.get",
        "System.ReadOnlySpan<T>.ToArray()", // Creates copy - pure
        "System.ReadOnlySpan<T>.Slice(int, int)", // Pure view
        "System.Span<T>.Length.get", // Added corresponding Span<T>
        "System.Span<T>.IsEmpty.get", // Added corresponding Span<T>

        // --- ADDED: System.String (Common Pure Methods) ---
        "string.Clone()",
        "string.CompareTo(string)",
        "string.Contains(string)",
        "string.EndsWith(string)",
        "string.Equals(string)", // Static and instance overloads
        "string.Equals(string, string)",
        "string.Equals(string, System.StringComparison)",
        "string.GetHashCode()",
        "string.IndexOf(char)", // Common overload
        "string.IndexOf(string)", // Common overload
        "string.IsNullOrEmpty(string)",
        "string.IsNullOrWhiteSpace(string)",
        "string.Length.get",
        "string.StartsWith(string)",
        "string.Substring(int)", // Common overload
        "string.Substring(int, int)",
        "string.ToCharArray()",
        "string.ToLower()",
        "string.ToLowerInvariant()",
        "string.ToString()", // Returns self
        "string.ToUpper()",
        "string.ToUpperInvariant()",
        "string.Trim()",
        "string.TrimEnd()",
        "string.TrimStart()",
        // Operator overloads are harder to list by signature, handled by Binary/Unary rules maybe
        // "string.op_Equality(string, string)",
        // "string.op_Inequality(string, string)",
        // -----------------------------------------------

        // System.Text.Encoding
        "System.Text.Encoding.UTF8.get", // Static property access
        "System.Text.Encoding.GetString(byte[])", // Common overload
    };
}