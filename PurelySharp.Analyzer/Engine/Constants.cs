using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine;

public static class Constants
{

    public static readonly ImmutableHashSet<string> KnownImpureNamespaces = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.IO",
        "System.Net",
        "System.Data",
        "System.Threading",
        "System.Diagnostics",
        "System.Security.Cryptography",
        "System.Runtime.InteropServices",
        "System.Reflection"
    );

    public static readonly ImmutableHashSet<string> KnownImpureTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Random",
        "System.DateTime",
        "System.Guid",
        "System.Console",
        "System.Environment",
        "System.Timers.Timer"


    );

    public static readonly HashSet<string> KnownImpureMethods = new HashSet<string>(StringComparer.Ordinal)
    {

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
        "System.Console.Write(string)",
        "System.Console.WriteLine(string)",
        "System.Console.Write(object)",
        "System.Console.WriteLine(object)",
        "System.Console.Write()",
        "System.Console.WriteLine()",
        "System.DateTime.Now.get",
        "System.DateTime.UtcNow.get",
        "System.DateTimeOffset.Now.get",
        "System.DateTimeOffset.UtcNow.get",
        "System.Diagnostics.ActivitySource.StartActivity(string)",
        "System.Diagnostics.Debug.WriteLine(string)",
        "System.Diagnostics.Debugger.Break()",
        "System.Diagnostics.Process.GetCurrentProcess()",
        "System.Diagnostics.Process.Start(string)",
        "System.Diagnostics.Stopwatch.Elapsed.get",
        "System.Diagnostics.Stopwatch.GetTimestamp()",
        "System.Diagnostics.Stopwatch.Start()",
        "System.Diagnostics.Stopwatch.Stop()",
        "System.Diagnostics.Trace.WriteLine(string)",
        "System.Environment.CurrentDirectory.get",
        "System.Environment.CurrentDirectory.set",
        "System.Environment.Exit(int)",
        "System.Environment.GetEnvironmentVariable(string)",
        "System.Environment.GetFolderPath(System.Environment.SpecialFolder)",
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
        "System.IO.DriveInfo.TotalSize.get",
        "System.IO.DriveInfo.GetDrives()",
        "System.IO.File.AppendAllText(string, string)",
        "System.IO.File.Delete(string)",
        "System.IO.File.Exists(string)",
        "System.IO.File.ReadAllBytes(string)",
        "System.IO.File.ReadAllText(string)",
        "System.IO.File.WriteAllText(string, string)",
        "System.IO.File.WriteAllBytes(string, byte[])",
        "System.IO.MemoryStream.Write(byte[], int, int)",
        "System.IO.Path.GetRandomFileName()",
        "System.IO.Path.GetTempFileName()",
        "System.IO.Path.GetTempPath()",
        "System.IO.Stream.Flush()",
        "System.IO.Stream.Read(byte[], int, int)",
        "System.IO.Stream.Seek(long, System.IO.SeekOrigin)",
        "System.IO.Stream.Write(byte[], int, int)",
        "System.IO.StreamReader.ReadLine()",
        "System.IO.StreamReader.StreamReader(System.IO.Stream)",
        "System.IO.StreamWriter.WriteLine(string)",
        "System.IO.StreamWriter.StreamWriter(System.IO.Stream)",
        "System.IO.StringReader.ReadToEnd()",
        "System.IO.StringWriter.Write(string)",
        "System.Lazy<T>.Value.get",
        "System.Linq.Enumerable.ToArray<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.ToDictionary<TSource, TKey>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TKey>)",
        "System.Linq.Enumerable.ToList<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Net.Dns.GetHostEntry(string)",
        "System.Net.Http.HttpClient.GetAsync(string)",
        "System.Net.Http.HttpClient.GetStringAsync(string)",
        "System.Net.Http.HttpClient.PostAsync(string, System.Net.Http.HttpContent)",
        "System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()",
        "System.Net.Sockets.Socket.Connect(System.Net.EndPoint)",
        "System.Net.Sockets.Socket.ConnectAsync(System.Net.EndPoint)",
        "System.Net.Sockets.Socket.Receive(byte[])",
        "System.Net.Sockets.Socket.Send(byte[])",
        "System.Net.WebClient.DownloadString(string)",
        "System.Random.Next()",
        "System.Random.Next(int)",
        "System.Random.NextDouble()",
        "System.Reflection.Assembly.Load(string)",
        "System.Reflection.Assembly.LoadFrom(string)",
        "System.Reflection.FieldInfo.SetValue(object, object)",
        "System.Reflection.MethodBase.GetCurrentMethod()",
        "System.Reflection.MethodInfo.Invoke(object, object[])",
        "System.Reflection.PropertyInfo.SetValue(object, object)",
        "System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)",
        "System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(System.RuntimeTypeHandle)",
        "System.Runtime.GCSettings.IsServerGC.get",
        "System.Runtime.InteropServices.Marshal.AllocHGlobal(System.IntPtr)",
        "System.Runtime.InteropServices.Marshal.FreeHGlobal(System.IntPtr)",
        "System.Runtime.InteropServices.Marshal.StructureToPtr(object, System.IntPtr, bool)",

        "System.Security.Cryptography.RandomNumberGenerator.GetBytes(byte[])",

        "System.Text.Json.JsonSerializer.Deserialize",
        "JsonSerializer.Deserialize",
        "System.Text.Json.JsonSerializer.Deserialize<TValue>(string, System.Text.Json.JsonSerializerOptions?)",
        "System.Text.Json.JsonSerializer.Deserialize<TValue>(System.ReadOnlySpan<byte>, System.Text.Json.JsonSerializerOptions?)",

        "System.Text.Json.JsonSerializer.DeserializeAsync",
        "System.Text.Json.JsonSerializer.SerializeAsync",
        "System.Text.StringBuilder.Append(string?)",
        "System.Text.StringBuilder.Append(char)",
        "System.Text.StringBuilder.Append(object)",
        "System.Text.StringBuilder.AppendLine(string)",
        "System.Text.StringBuilder.Clear()",
        "System.Text.StringBuilder.EnsureCapacity(int)",
        "System.Text.StringBuilder.Insert(int, string)",
        "System.Text.StringBuilder.Remove(int, int)",
        "System.Text.StringBuilder.Replace(string, string)",
        "System.Threading.Interlocked.CompareExchange(ref int, int, int)",
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
        "System.Threading.Tasks.Task.Run(System.Action)",


        "System.Threading.Tasks.Task.Yield()",
        "System.Threading.Thread.CurrentThread.get",
        "System.Threading.Thread.ManagedThreadId.get",
        "System.Threading.Thread.Sleep(int)",
        "System.Threading.Thread.Sleep(System.TimeSpan)",
        "System.Threading.Volatile.Write",
        "System.TimeZoneInfo.FindSystemTimeZoneById(string)",
        "System.Type.GetType(string)",
        "System.Xml.Linq.XElement.Add(object)",
        "System.Xml.Linq.XElement.Load(System.IO.Stream)",
        "System.Xml.Linq.XElement.Save(System.IO.Stream)",
        "System.Xml.Linq.XNode.Remove()",
        "System.Xml.XmlReader.Create(System.IO.Stream)",
        "System.Xml.XmlReader.Read()",
        "System.Xml.XmlWriter.Create(System.IO.Stream)",
        "System.Xml.XmlWriter.WriteStartElement(string)",
        "System.Xml.XmlWriter.WriteString(string)",
        "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>.TryAdd(TKey, TValue)",
        "System.Collections.Concurrent.ConcurrentQueue<T>.Enqueue(T)",
        "System.Collections.Concurrent.ConcurrentQueue<T>.TryDequeue(out T)",
        "System.Collections.Concurrent.BlockingCollection<T>.Add(T)",
        "System.Collections.Concurrent.BlockingCollection<T>.Take()",
        "System.Collections.ObjectModel.ObservableCollection<T>.Add(T)",
        "System.ComponentModel.BackgroundWorker.RunWorkerAsync()",
        "System.Diagnostics.EventLog.WriteEntry(string)",
        "System.Diagnostics.PerformanceCounter.NextValue()",
        "System.Diagnostics.TraceSource.TraceEvent(System.Diagnostics.TraceEventType, int)",
        "System.Diagnostics.FileVersionInfo.GetVersionInfo(string)",
        "System.Globalization.NumberFormatInfo.CurrentInfo.get",
        "System.IO.Compression.ZipFile.CreateFromDirectory(string, string)",
        "System.IO.Compression.ZipFile.ExtractToDirectory(string, string)",
        "System.IO.Pipes.NamedPipeServerStream.WaitForConnection()",
        "System.Net.Mail.SmtpClient.Send(System.Net.Mail.MailMessage)",
        "System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()",
        "System.Net.NetworkInformation.Ping.Send(string)",
        "System.Reflection.Emit.DynamicMethod.DynamicMethod(string, System.Type, System.Type[])",
        "System.Reflection.Emit.ILGenerator.Emit(System.Reflection.Emit.OpCode)",
        "System.Runtime.Caching.MemoryCache.Default.get",
        "System.Runtime.Caching.MemoryCache.Add(string, object, System.DateTimeOffset)",
        "System.Runtime.Caching.MemoryCache.Get(string)",
        "System.Runtime.Loader.AssemblyLoadContext.LoadFromAssemblyPath(string)",
        "System.Runtime.Serialization.Json.DataContractJsonSerializer.ReadObject(System.IO.Stream)",
        "System.Runtime.Serialization.Json.DataContractJsonSerializer.WriteObject(System.IO.Stream, object)",
        "System.Security.Principal.WindowsIdentity.GetCurrent()",
        "System.Security.SecureString.AppendChar(char)",
        "System.Security.SecureString.Dispose()",
        "System.Threading.CancellationToken.Register(System.Action)",
        "System.Threading.CancellationToken.ThrowIfCancellationRequested()",
        "System.Threading.CancellationTokenSource.Cancel()",
        "System.Threading.ReaderWriterLockSlim.EnterReadLock()",
        "System.Threading.ReaderWriterLockSlim.ExitReadLock()",
        "System.Threading.SpinWait.SpinOnce()",
        "System.Threading.Timer.Timer(System.Threading.TimerCallback)",
        "System.Threading.Timer.Change(int, int)",
        "System.Timers.Timer.Start()",
        "System.Timers.Timer.Stop()",
        "System.Xml.Xsl.XslCompiledTransform.Load(string)",
        "System.Xml.Xsl.XslCompiledTransform.Transform(string, string)",
        "System.Collections.BitArray.Set(int, bool)",
        "System.Collections.Specialized.NameValueCollection.Add(string, string)",
        "System.Diagnostics.Process.GetProcessesByName(string)",
        "System.IO.DirectoryInfo.Exists.get",
        "System.IO.DirectoryInfo.EnumerateFiles()",
        "System.IO.FileInfo.Length.get",
        "System.IO.FileSystemWatcher.EnableRaisingEvents.set",
        "System.Net.HttpListener.Start()",
        "System.Net.HttpListener.GetContext()",
        "System.Net.Sockets.UdpClient.Receive(ref System.Net.IPEndPoint)",
        "System.Reflection.AssemblyName.GetAssemblyName(string)",
        "System.Security.Cryptography.X509Certificates.X509Store.Open(System.Security.Cryptography.X509Certificates.OpenFlags)",
        "System.Threading.Barrier.SignalAndWait()",
        "System.Threading.CountdownEvent.Signal()",
        "System.Threading.CountdownEvent.Wait()",
        "System.Threading.Tasks.Dataflow.ActionBlock<TInput>.Post(TInput)",
        "System.Threading.Tasks.Parallel.ForEach",
        "System.Threading.Tasks.Parallel.Invoke",
        "System.Windows.Input.ICommand.Execute(object)",
        "Microsoft.Extensions.Logging.ILogger.LogInformation(string)",
        "Microsoft.Extensions.DependencyInjection.ServiceProvider.GetService(System.Type)",
        "System.IO.BufferedStream.BufferedStream(System.IO.Stream)",
        "System.IO.BufferedStream.Flush()",


        "string.Format(string, object?, object?)",












        "Microsoft.Extensions.Configuration.IConfiguration.GetConnectionString(string)",
        "Microsoft.Extensions.Configuration.IConfigurationRoot.Reload()",
        "System.Buffers.ArrayPool<T>.Shared.Rent(int)",
        "System.Buffers.ArrayPool<T>.Shared.Return(T[], bool)",
        "System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(System.Span<byte>, ulong)",
        "System.Buffers.Text.Base64.EncodeToUtf8(System.ReadOnlySpan<byte>, System.Span<byte>, out int, out int)",
        "System.Collections.Generic.LinkedList<T>.AddFirst(T)",
        "System.Collections.Generic.LinkedListNode<T>.Value.set",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Add(TKey, TValue)",








        "System.Collections.Generic.KeyedCollection<TKey, TItem>.Remove(TKey)",
        "System.ComponentModel.CancelEventArgs.Cancel.set",
        "System.ComponentModel.INotifyPropertyChanged.PropertyChanged",
        "System.Data.DataTable.NewRow()",
        "System.Data.DataRow.AcceptChanges()",
        "System.Diagnostics.Activity.Current.get",
        "System.Diagnostics.Activity.Current.set",
        "System.Diagnostics.Activity.SetTag(string, object)",
        "System.Diagnostics.DiagnosticListener.Write(string, object)",
        "System.Drawing.Bitmap.Bitmap(int, int)",
        "System.IO.Compression.BrotliStream.BrotliStream(System.IO.Stream, System.IO.Compression.CompressionMode)",
        "System.IO.Compression.DeflateStream.Read(byte[], int, int)",
        "System.IO.MemoryMappedFiles.MemoryMappedFile.CreateFromFile(string)",
        "System.IO.MemoryMappedFiles.MemoryMappedViewAccessor.ReadByte(long)",
        "System.Linq.Queryable.Count<TSource>(System.Linq.IQueryable<TSource>)",
        "System.Linq.Queryable.ToList<TSource>(System.Linq.IQueryable<TSource>)",
        "System.Net.Http.Headers.HttpRequestHeaders.Add(string, string)",
        "System.Net.Security.SslStream.AuthenticateAsClientAsync(string)",
        "System.Net.Sockets.Socket.Accept()",
        "System.Net.Sockets.SocketAsyncEventArgs.AcceptSocket.set",
        "System.Reflection.Emit.AssemblyBuilder.DefineDynamicModule(string)",
        "System.Resources.ResourceManager.GetString(string)",
        "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start<TStateMachine>(ref TStateMachine)",
        "System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>.Add(TKey, TValue)",
        "System.Runtime.InteropServices.ComWrappers.GetOrCreateObjectForComInstance(System.IntPtr, System.Runtime.InteropServices.CreateObjectFlags)",
        "System.Runtime.InteropServices.GCHandle.Alloc(object)",
        "System.Runtime.InteropServices.GCHandle.Free()",
        "System.Security.AccessControl.DirectorySecurity.AddAccessRule(System.Security.AccessControl.FileSystemAccessRule)",
        "System.Security.Cryptography.Pkcs.SignedCms.Sign()",
        "System.Security.Cryptography.Xml.SignedXml.ComputeSignature()",
        "System.Security.Cryptography.X509Certificates.X509Certificate2.X509Certificate2(string)",
        "System.ServiceProcess.ServiceBase.Run(System.ServiceProcess.ServiceBase)",
        "System.Speech.Synthesis.SpeechSynthesizer.SpeakAsync(string)",
        "System.Text.Json.Utf8JsonWriter.WriteString(string, string)",
        "System.Threading.AsyncLocal<T>.Value.get",
        "System.Threading.AsyncLocal<T>.Value.set",
        "System.Threading.Channels.ChannelReader<T>.ReadAsync(System.Threading.CancellationToken)",
        "System.Threading.Channels.ChannelWriter<T>.WriteAsync(T, System.Threading.CancellationToken)",
        "System.Threading.LazyInitializer.EnsureInitialized<T>(ref T, System.Func<T>)",
        "System.Transactions.TransactionScope.TransactionScope()",
        "System.Transactions.Transaction.Current.get",
        "Microsoft.Win32.RegistryKey.OpenSubKey(string)",
        "Microsoft.Win32.RegistryKey.GetValue(string)",
        "Microsoft.Win32.RegistryKey.SetValue(string, object)",
        "System.Net.Http.Headers.HttpContentHeaders.ContentLength.set",
        "System.Runtime.InteropServices.SafeHandle.Dispose()",
        "System.Text.Unicode.Utf8.ToUtf16(System.ReadOnlySpan<byte>, System.Span<char>, out int, out int)",
        "System.Threading.Semaphore.Semaphore(int, int)",
        "System.TimeZoneInfo.ClearCachedData()",
        "System.AppContext.SetSwitch(string, bool)",
        "System.Collections.Generic.PriorityQueue<TElement, TPriority>.Enqueue(TElement, TPriority)",
        "System.Collections.Generic.PriorityQueue<TElement, TPriority>.Dequeue()",
        "System.Diagnostics.Metrics.Counter<T>.Add(T, System.Collections.Generic.KeyValuePair<string, object?>)",
        "System.Runtime.InteropServices.MemoryMarshal.Write<T>(System.Span<byte>, ref T)",
        "System.ComponentModel.Component.Dispose()",
        "System.ComponentModel.LicenseManager.Validate(System.Type, object)",
        "System.Configuration.ConfigurationManager.AppSettings.get",
        "System.Console.Beep()",
        "System.Console.BufferHeight.get",
        "System.Console.BufferHeight.set",
        "System.Console.Title.get",
        "System.Console.Title.set",
        "System.Data.DataSet.Clear()",
        "System.Diagnostics.Debugger.IsAttached.get",
        "System.Diagnostics.Debugger.Launch()",
        "System.Diagnostics.StackTrace.StackTrace()",
        "System.Diagnostics.Switch.Level.get",
        "System.DirectoryServices.DirectoryEntry.DirectoryEntry(string)",
        "System.GC.GetGeneration(object)",
        "System.GC.KeepAlive(object)",
        "System.Globalization.DateTimeFormatInfo.CurrentInfo.get",
        "System.IO.BinaryReader.ReadBoolean()",
        "System.IO.BinaryWriter.Write(string)",
        "System.IO.Directory.EnumerateDirectories(string)",
        "System.IO.Directory.GetCurrentDirectory()",
        "System.IO.Directory.SetCurrentDirectory(string)",
        "System.IO.FileStream.FileStream(string, System.IO.FileMode)",
        "System.IO.Pipelines.PipeReader.ReadAsync(System.Threading.CancellationToken)",
        "System.IO.Pipelines.PipeWriter.WriteAsync(System.ReadOnlyMemory<byte>, System.Threading.CancellationToken)",
        "System.Linq.ParallelEnumerable.ForAll<TSource>(System.Linq.ParallelQuery<TSource>, System.Action<TSource>)",
        "System.Linq.ParallelQuery<TSource>.ToList()",
        "System.Management.ManagementObjectSearcher.ManagementObjectSearcher(string)",
        "System.Net.CredentialCache.DefaultCredentials.get",
        "System.Net.Http.HttpMessageInvoker.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)",
        "System.Net.ServicePointManager.SecurityProtocol.get",
        "System.Net.ServicePointManager.SecurityProtocol.set",
        "System.Runtime.InteropServices.Marshal.GetLastWin32Error()",
        "System.Runtime.Serialization.FormatterServices.GetUninitializedObject(System.Type)",
        "System.Threading.ThreadLocal<T>.Value.get",


        "System.Collections.Generic.IEnumerator<T>.MoveNext()",
        "System.Collections.ObjectModel.Collection<T>.InsertItem(int, T)",
        "System.Collections.ObjectModel.Collection<T>.SetItem(int, T)",
        "System.ComponentModel.INotifyCollectionChanged.CollectionChanged",
        "System.DateTime.ToLocalTime()",
        "System.Delegate.DynamicInvoke(object[])",
        "System.Environment.OSVersion.get",

        "System.GC.SuppressFinalize(object)",

        "System.IDisposable.Dispose()",
        "System.IServiceProvider.GetService(System.Type)",
        "System.IO.File.Copy(string, string)",
        "System.IO.File.Move(string, string)",
        "System.IO.File.OpenRead(string)",
        "System.IO.File.OpenWrite(string)",
        "System.IO.File.ReadAllLines(string)",
        "System.IO.Stream.Close()",
        "System.IO.Stream.CopyToAsync(System.IO.Stream)",
        "System.IO.TextReader.Peek()",
        "System.IO.TextReader.ReadToEnd()",
        "System.IO.TextWriter.Flush()",
        "System.IO.TextWriter.Write(char)",
        "System.Net.Http.HttpContent.ReadAsStringAsync()",
        "System.Net.Http.HttpContent.ReadAsByteArrayAsync()",
        "System.Text.Encoding.Default.get",
        "System.Threading.Tasks.Task.ContinueWith(System.Action<System.Threading.Tasks.Task>)",
        "System.Threading.Tasks.Task.Wait()",
        "System.Threading.Tasks.Task<TResult>.Result.get",


        "System.Array.Clear(System.Array, int, int)",
        "System.Array.ConstrainedCopy(System.Array, int, System.Array, int, int)",
        "System.Array.Copy(System.Array, System.Array, int)",
        "System.Array.Resize<T>(ref T[], int)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Clear()",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Remove(TKey)",
        "System.Collections.Generic.HashSet<T>.Add(T)",
        "System.Collections.Generic.HashSet<T>.Clear()",
        "System.Collections.Generic.HashSet<T>.Remove(T)",
        "System.Collections.Generic.List<T>.AddRange(System.Collections.Generic.IEnumerable<T>)",
        "System.Collections.Generic.List<T>.Capacity.set",
        "System.Collections.Generic.List<T>.InsertRange(int, System.Collections.Generic.IEnumerable<T>)",
        "System.Collections.Generic.List<T>.RemoveAll(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.RemoveAt(int)",
        "System.Collections.Generic.List<T>.RemoveRange(int, int)",
        "System.Collections.Generic.List<T>.Reverse()",
        "System.Collections.Generic.List<T>.Sort()",
        "System.Collections.Generic.Queue<T>.Clear()",
        "System.Collections.Generic.Stack<T>.Clear()",
        "System.Exception.Source.set",
        "System.IO.Path.GetFullPath(string)",


        "System.Activator.CreateInstanceFrom(string, string)",
        "System.Array.Clear(System.Array, int, int)",
        "System.Array.ConstrainedCopy(System.Array, int, System.Array, int, int)",
        "System.Array.Copy(System.Array, System.Array, int)",
        "System.Array.Sort<T>(T[], System.Comparison<T>)",
        "System.Array.Resize<T>(ref T[], int)",
        "System.Collections.Concurrent.ConcurrentBag<T>.Add(T)",
        "System.Collections.Concurrent.ConcurrentBag<T>.TryTake(out T)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.TryAdd(TKey, TValue)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Values.CopyTo(TValue[], int)",
        "System.Collections.Generic.ICollection<T>.Add(T)",
        "System.Collections.Generic.ICollection<T>.Clear()",
        "System.Collections.Generic.ICollection<T>.Remove(T)",
        "System.Collections.Generic.IList<T>.Insert(int, T)",
        "System.Collections.Generic.IList<T>.RemoveAt(int)",
        "System.Collections.Generic.SortedSet<T>.Add(T)",
        "System.ComponentModel.EventHandlerList.AddHandler(object, System.Delegate)",
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
        "System.HashCode.Add<T>(T)",
        "System.IO.DirectoryInfo.Create()",
        "System.IO.DirectoryInfo.Delete()",
        "System.IO.FileInfo.CopyTo(string)",
        "System.IO.FileInfo.Delete()",
        "System.IO.Stream.ReadAsync(byte[], int, int, System.Threading.CancellationToken)",
        "System.IO.Stream.WriteAsync(byte[], int, int, System.Threading.CancellationToken)",
        "System.IO.StreamReader.StreamReader(string)",
        "System.IO.StreamWriter.StreamWriter(string)",
        "System.Linq.Enumerable.ToLookup",
        "System.Text.StringBuilder.AppendJoin(string, object[])",
        "System.Threading.Monitor.TryEnter(object)",


        "System.Xml.Linq.XContainer.Add(object)",
        "System.Xml.Linq.XElement.Add(object)",


        "System.Threading.Tasks.ValueTask<TResult>.ValueTask(TResult)",
        "System.Threading.Tasks.Task.Run(System.Action)",
        "System.IO.MemoryStream.MemoryStream()",

        "System.Text.Json.JsonSerializer.Deserialize",
        "System.Text.Json.JsonSerializer.Serialize",

        "System.Text.Json.JsonSerializer.DeserializeAsync",
        "System.Text.Json.JsonSerializer.SerializeAsync",
        "System.Text.StringBuilder.Append(string?)",
        "System.Security.Cryptography.RandomNumberGenerator.Fill(byte[])",



        "System.Text.StringBuilder.Append(string?)",
























        "System.Convert.ChangeType(object, System.Type)",
    };

    public static readonly HashSet<string> KnownPureBCLMembers = new HashSet<string>(StringComparer.Ordinal)
    {

        "System.Array.ConvertAll<TInput, TOutput>(TInput[], System.Converter<TInput, TOutput>)",
        "System.Array.Empty<T>()",
        "System.Array.Exists<T>(T[], System.Predicate<T>)",
        "System.Array.IndexOf(System.Array, object)",
        "System.Array.TrueForAll<T>(T[], System.Predicate<T>)",
        "System.Array.Length.get",


        "System.Attribute.GetCustomAttribute(System.Reflection.MemberInfo, System.Type)",


        "System.BitConverter.GetBytes(int)",
        "System.BitConverter.GetBytes(double)",
        "System.BitConverter.ToInt32(byte[], int)",
        "System.BitConverter.ToDouble(byte[], int)",


        "bool.Parse(string)",
        "bool.ToString()",


        "char.IsDigit(char)",
        "char.IsLetter(char)",
        "char.IsWhiteSpace(char)",
        "char.ToLowerInvariant(char)",
        "char.ToUpperInvariant(char)",
        "char.ToString()",


        "System.Collections.Generic.Comparer<T>.Default.get",
        "System.Collections.Generic.Dictionary<TKey, TValue>()",
        "System.Collections.Generic.Dictionary<TKey, TValue>.ContainsKey(TKey)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.ContainsValue(TValue)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.TryGetValue(TKey, out TValue)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Count.get",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Keys.get",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Values.get",
        "System.Collections.Generic.EqualityComparer<T>.Default.get",
        "System.Collections.Generic.HashSet<T>.IsSubsetOf(System.Collections.Generic.IEnumerable<T>)",
        "System.Collections.Generic.List<T>()",
        "System.Collections.Generic.List<T>.Contains(T)",
        "System.Collections.Generic.List<T>.Count.get",
        "System.Collections.Generic.List<T>.Find(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.Exists(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.TrueForAll(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.this[int].get",
        "System.Collections.Generic.Queue<T>.Peek()",
        "System.Collections.Generic.Stack<T>.Peek()",


        "System.Collections.Immutable.ImmutableList<T>.Add(T)",
        "System.Collections.Immutable.ImmutableList<T>.Contains(T)",
        "System.Collections.Immutable.ImmutableList<T>.Count.get",
        "System.Collections.Immutable.ImmutableList<T>.Remove(T)",
        "System.Collections.Immutable.ImmutableList<T>.SetItem(int, T)",

        "System.Collections.Immutable.ImmutableList<T>.this[int].get",
        "System.Collections.Immutable.ImmutableList.Create<T>()",
        "System.Collections.Immutable.ImmutableList.Create<T>(params T[])",
        "System.Collections.Immutable.ImmutableArray.Create<T>()",
        "System.Collections.Immutable.ImmutableArray.Create<T>(params T[])",
        "System.Collections.Immutable.ImmutableDictionary.Create<TKey, TValue>()",
        "System.Collections.Immutable.ImmutableHashSet.Create<T>()",


        "System.ComponentModel.TypeDescriptor.GetProperties(object)",


        "System.Convert.FromBase64String(string)",
        "System.Convert.ToBase64String(byte[])",
        "System.Convert.ToDouble(object)",
        "System.Convert.ToInt32(object)",
        "System.Convert.ToString(object)",


        "System.DateTime.DateTime(long)",
        "System.DateTime.DateTime(int, int, int)",
        "System.DateTime.AddDays(double)",
        "System.DateTime.IsLeapYear(int)",
        "System.DateTime.ToString()",
        "System.DateTimeOffset.DateTimeOffset(long, System.TimeSpan)",
        "System.DateTimeOffset.FromUnixTimeMilliseconds(long)",
        "System.DateTimeOffset.ToUnixTimeSeconds()",
        "System.DateTimeOffset.ToString()",


        "System.DBNull.Value.get",


        "System.Diagnostics.Contracts.Contract.Ensures(bool)",
        "System.Diagnostics.Contracts.Contract.Requires(bool)",


        "System.Diagnostics.Stopwatch.Stopwatch()",


        "double.Parse(string)",
        "double.ToString()",


        "System.Enum.GetName(System.Type, object)",
        "System.Enum.GetValues(System.Type)",
        "System.Enum.IsDefined(System.Type, object)",


        "System.Globalization.CultureInfo.GetCultureInfo(string)",
        "System.Globalization.CultureInfo.InvariantCulture.get",


        "System.Guid.Guid(byte[])",
        "System.Guid.Parse(string)",
        "System.Guid.ToString()",


        "int.Parse(string)",
        "int.ToString()",


        "System.IO.MemoryStream.ToArray()",
        "System.IO.Path.Combine(string, string)",
        "System.IO.Path.GetDirectoryName(string)",
        "System.IO.Path.GetFileName(string)",
        "System.IO.StringReader.StringReader(string)",
        "System.IO.StringWriter()",


        "System.Lazy<T>.Lazy(System.Func<T>)",


        "System.Linq.Enumerable.Aggregate",
        "System.Linq.Enumerable.All<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)",
        "System.Linq.Enumerable.Any<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Cast<TResult>(System.Collections.IEnumerable)",
        "System.Linq.Enumerable.Count<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Empty<TResult>()",
        "System.Linq.Enumerable.FirstOrDefault<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.GroupBy",
        "System.Linq.Enumerable.OfType<TResult>(System.Collections.IEnumerable)",
        "System.Linq.Enumerable.OrderBy",
        "System.Linq.Enumerable.Range(int, int)",
        "System.Linq.Enumerable.Repeat<TResult>(TResult, int)",
        "System.Linq.Enumerable.Select<TSource, TResult>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, TResult>)",
        "System.Linq.Enumerable.SequenceEqual<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Sum",
        "System.Linq.Enumerable.Where<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)",





        "System.Linq.Enumerable.Contains<TSource>(System.Collections.Generic.IEnumerable<TSource>, TSource)",



        "System.Math.Abs(double)",
        "System.Math.Ceiling(double)",
        "System.Math.Clamp",
        "System.Math.Floor(double)",
        "System.Math.Max",
        "System.Math.Min",
        "System.Math.Round",
        "System.Math.Sin(double)",
        "System.Math.Sqrt(double)",


        "System.MemoryExtensions.SequenceEqual<T>(System.ReadOnlySpan<T>, System.ReadOnlySpan<T>)",
        "System.MemoryExtensions.Trim<T>(System.ReadOnlySpan<T>)",


        "System.Net.Http.HttpClient()",
        "System.Net.IPAddress.Loopback.get",
        "System.Net.IPAddress.Parse(string)",
        "System.Net.WebUtility.HtmlEncode(string)",
        "System.Net.WebUtility.UrlDecode(string)",


        "System.Nullable.Compare<T>(T?, T?)",
        "System.Nullable.Equals<T>(T?, T?)",


        "System.Numerics.BigInteger.Add(System.Numerics.BigInteger, System.Numerics.BigInteger)",
        "System.Numerics.BigInteger.Parse(string)",
        "System.Numerics.Complex.Complex(double, double)",
        "System.Numerics.Complex.Abs(System.Numerics.Complex)",


        "object.Equals(object, object)",
        "object.GetHashCode()",
        "object.GetType()",
        "object.ReferenceEquals(object, object)",
        "object.ToString()",


        "System.OperatingSystem.IsWindows()",


        "System.Reflection.Assembly.GetExecutingAssembly()",
        "System.Reflection.Assembly.GetTypes()",
        "System.Reflection.FieldInfo.GetValue(object)",
        "System.Reflection.PropertyInfo.GetValue(object)",
        "System.Reflection.TypeInfo.GetMethods()",
        "System.Reflection.TypeInfo.GetProperties()",


        "System.Runtime.InteropServices.Marshal.PtrToStructure<T>(System.IntPtr)",


        "System.Security.Claims.ClaimsPrincipal.IsInRole(string)",


        "System.Security.Cryptography.Aes.DecryptCbc(byte[], byte[], byte[], System.Security.Cryptography.PaddingMode)",
        "System.Security.Cryptography.Aes.EncryptCbc(byte[], byte[], byte[], System.Security.Cryptography.PaddingMode)",
        "System.Security.Cryptography.MD5.ComputeHash(byte[])",
        "System.Security.Cryptography.SHA256.ComputeHash(byte[])",


        "string.Concat(string, string)",
        "string.Concat(params string[])",
        "string.IsNullOrEmpty(string)",
        "string.IsNullOrWhiteSpace(string)",
        "string.Replace(string, string)",
        "string.Substring(int, int)",
        "string.ToLower()",
        "string.ToUpper()",
        "string.Trim()",
        "string.Length.get",
        "string.Equals(string)",
        "string.Equals(object)",
        "string.GetHashCode()",
        "string.ToLowerInvariant()",
        "string.ToUpperInvariant()",

        "System.String.Split(char)",
        "System.String.Split(params char[])",
        "System.String.Split(char[])",
        "System.String.Split(char[], System.StringSplitOptions)",
        "System.String.Split(string[], System.StringSplitOptions)",
        "System.String.Split(char[], int, System.StringSplitOptions)",
        "System.String.Split(string[], int, System.StringSplitOptions)",

        "System.String.Join(string, string[])",
        "System.String.Join(string, params string[])",
        "System.String.Join(string, System.Collections.Generic.IEnumerable<string>)",
        "System.String.Join<T>(string, System.Collections.Generic.IEnumerable<T>)",


        "string.String(System.ReadOnlySpan<char>)",



        "System.StringComparer.InvariantCultureIgnoreCase.Compare(string, string)",
        "System.StringComparer.Ordinal.Equals(string, string)",


        "System.Text.Encoding.GetBytes(string)",
        "System.Text.Encoding.GetEncoding(string)",
        "System.Text.Encoding.GetString(byte[])",
        "System.Text.Encoding.UTF8.get",
        "System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, System.Text.Json.JsonSerializerOptions?)",
        "System.Text.RegularExpressions.Regex.Regex(string)",
        "System.Text.RegularExpressions.Regex.IsMatch(string, string)",
        "System.Text.RegularExpressions.Regex.Match(string, string)",
        "System.Text.RegularExpressions.Regex.Replace(string, string, string)",
        "System.Text.RegularExpressions.Regex.Split(string, string)",
        "System.Text.RegularExpressions.Regex.IsMatch(string)",
        "System.Text.RegularExpressions.Regex.Match(string)",
        "System.Text.RegularExpressions.Regex.Replace(string, string)",
        "System.Text.RegularExpressions.Regex.Split(string)",
        "System.Text.StringBuilder.ToString()",




        "System.Threading.Tasks.Task.CompletedTask.get",
        "System.Threading.Tasks.Task.FromResult<TResult>(TResult)",
        "System.Threading.Volatile.Read",


        "System.TimeSpan.TimeSpan(long)",
        "System.TimeSpan.Add(System.TimeSpan)",
        "System.TimeSpan.ToString()",


        "System.TimeZoneInfo.ConvertTime(System.DateTimeOffset, System.TimeZoneInfo)",


        "System.Tuple.Create",
        "System.ValueTuple.Create",


        "System.Type.Equals(object)",
        "System.Type.Equals(System.Type)",
        "System.Type.GetHashCode()",
        "System.Type.ToString()",



        "System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(System.ReadOnlySpan<byte>)",
        "System.Buffers.Text.Utf8Parser.TryParse(System.ReadOnlySpan<byte>, out int, out int)",
        "System.Collections.Generic.LinkedListNode<T>.Value.get",
        "System.Collections.Generic.SortedList<TKey, TValue>.IndexOfKey(TKey)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsKey(TKey)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.ContainsValue(TValue)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.TryGetValue(TKey, out TValue)",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Count.get",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Keys.get",
        "System.Collections.Generic.SortedDictionary<TKey, TValue>.Values.get",
        "System.Collections.Generic.KeyedCollection<TKey, TItem>.Contains(TKey)",
        "System.ComponentModel.AddingNewEventArgs.AddingNewEventArgs()",
        "System.ComponentModel.CancelEventArgs.Cancel.get",
        "System.Drawing.Color.FromArgb(int, int, int, int)",
        "System.Drawing.Point.Point(int, int)",
        "System.Globalization.StringInfo.ParseCombiningCharacters(string)",
        "System.Linq.IQueryable<T>.Expression.get",
        "System.Linq.Queryable.Where<TSource>(System.Linq.IQueryable<TSource>, System.Linq.Expressions.Expression<System.Func<TSource, bool>>)",
        "System.Memory<T>.Span.get",
        "System.Memory<T>.Slice(int, int)",
        "System.Net.Sockets.SocketAsyncEventArgs.AcceptSocket.get",
        "System.Numerics.Matrix4x4.CreateRotationX(float)",
        "System.Numerics.Vector3.Normalize(System.Numerics.Vector3)",
        "System.Reflection.AssemblyName.AssemblyName(string)",
        "System.Reflection.CustomAttributeData.GetCustomAttributes(System.Reflection.MemberInfo)",
        "System.Reflection.Metadata.MetadataReader.GetString(System.Reflection.Metadata.StringHandle)",
        "System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>(ref TFrom)",
        "System.Runtime.CompilerServices.Unsafe.SizeOf<T>()",
        "System.Runtime.Intrinsics.X86.Sse.Add(System.Runtime.Intrinsics.Vector128<float>, System.Runtime.Intrinsics.Vector128<float>)",
        "System.Runtime.Intrinsics.X86.Avx2.Multiply(System.Runtime.Intrinsics.Vector256<double>, System.Runtime.Intrinsics.Vector256<double>)",
        "System.Runtime.Versioning.FrameworkName.FrameworkName(string)",
        "System.Security.Cryptography.Pkcs.SignedCms.Decode(byte[])",
        "System.Text.Json.JsonDocument.Parse(string, System.Text.Json.JsonDocumentOptions)",
        "System.Text.Json.JsonElement.GetString()",
        "System.Threading.Channels.Channel.CreateUnbounded<T>()",
        "System.Xml.Schema.XmlSchemaSet.Compile()",
        "System.Xml.XmlDocument.LoadXml(string)",
        "System.Xml.XmlDocument.SelectSingleNode(string)",
        "System.Diagnostics.CounterSample.Calculate(System.Diagnostics.CounterSample, System.Diagnostics.CounterSample)",
        "System.Net.Http.Headers.HttpContentHeaders.ContentLength.get",
        "System.Numerics.Plane.Normalize(System.Numerics.Plane)",
        "System.Reflection.Emit.Label.Equals(object)",
        "System.Runtime.InteropServices.SafeHandle.IsInvalid.get",
        "System.Threading.Tasks.ValueTask.AsTask()",
        "System.Buffers.ReadOnlySequence<T>.Slice(long)",
        "System.Diagnostics.Metrics.Meter.CreateCounter<T>(string, string, string)",
        "System.IO.Hashing.Crc32.Hash(System.ReadOnlySpan<byte>)",
        "System.Linq.Enumerable.Chunk<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)",
        "System.Runtime.InteropServices.MemoryMarshal.Read<T>(System.ReadOnlySpan<byte>)",
        "System.ArgumentNullException.ArgumentNullException(string)",
        "System.ArgumentOutOfRangeException.ArgumentOutOfRangeException(string)",
        "System.ArraySegment<T>.ArraySegment(T[], int, int)",
        "System.Attribute.GetCustomAttributes(System.Reflection.MemberInfo)",
        "System.AttributeUsageAttribute.AttributeUsageAttribute(System.AttributeTargets)",
        "System.BadImageFormatException.BadImageFormatException(string)",
        "System.BitOperations.LeadingZeroCount(uint)",
        "System.BitOperations.PopCount(ulong)",
        "System.CodeDom.Compiler.CodeDomProvider.CreateProvider(string)",
        "System.CodeDom.Compiler.CompilerResults.Errors.get",
        "System.Collections.ArrayList.Adapter(System.Collections.IList)",
        "System.Collections.Hashtable.ContainsKey(object)",
        "System.Collections.Queue.Synchronized(System.Collections.Queue)",
        "System.Collections.SortedList.GetKey(int)",
        "System.ComponentModel.AttributeCollection.GetDefaultAttribute<T>()",
        "System.ComponentModel.BrowsableAttribute.BrowsableAttribute(bool)",
        "System.ComponentModel.DataAnnotations.RangeAttribute.RangeAttribute(double, double)",
        "System.ComponentModel.DescriptionAttribute.DescriptionAttribute(string)",
        "System.Data.DataColumn.DataColumn(string)",
        "System.Data.DataRelation.DataRelation(string, System.Data.DataColumn, System.Data.DataColumn)",
        "System.Diagnostics.ConditionalAttribute.ConditionalAttribute(string)",
        "System.Diagnostics.Debug.Assert(bool)",
        "System.Diagnostics.StackFrame.GetMethod()",
        "System.Diagnostics.Stopwatch.IsRunning.get",
        "System.DivideByZeroException.DivideByZeroException()",
        "System.EventArgs.Empty.get",
        "System.Exception.HResult.get",
        "System.Exception.InnerException.get",
        "System.Exception.ToString()",
        "System.FlagsAttribute.FlagsAttribute()",
        "System.FormatException.FormatException(string)",
        "System.Globalization.CompareInfo.Compare(string, string)",
        "System.Half.Parse(string)",
        "System.HashCode.Combine<T1, T2>(T1, T2)",
        "System.Index.Index(int, bool)",
        "System.IO.EndOfStreamException.EndOfStreamException()",
        "System.IO.Path.ChangeExtension(string, string)",
        "System.IO.Path.HasExtension(string)",
        "System.IO.Pipelines.Pipe.Pipe(System.IO.Pipelines.PipeOptions)",
        "System.Linq.Expressions.Expression.Constant(object)",
        "System.Linq.Expressions.Expression.Call(System.Reflection.MethodInfo, System.Linq.Expressions.Expression[])",
        "System.Linq.ParallelEnumerable.AsParallel<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.MemoryExtensions.AsSpan<T>(T[])",
        "System.MemoryExtensions.BinarySearch<T>(System.ReadOnlySpan<T>, T)",
        "System.Net.Cookie.Cookie(string, string)",
        "System.Net.HttpVersion.Version11.get",
        "System.NotImplementedException.NotImplementedException()",
        "System.Nullable<T>.GetValueOrDefault()",
        "System.Numerics.Quaternion.Quaternion(float, float, float, float)",
        "System.ObsoleteAttribute.ObsoleteAttribute(string)",
        "System.OverflowException.OverflowException()",
        "System.PlatformNotSupportedException.PlatformNotSupportedException()",
        "System.Range.Range(System.Index, System.Index)",
        "System.Reflection.Emit.OpCodes.Ldarg_0.get",
        "System.Reflection.IntrospectionExtensions.GetTypeInfo(System.Type)",
        "System.Reflection.MemberInfo.Name.get",
        "System.Reflection.Missing.Value.get",
        "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute.CallerArgumentExpressionAttribute(string)",
        "System.Runtime.CompilerServices.IsExternalInit",
        "System.Runtime.CompilerServices.MethodImplAttribute.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions)",
        "System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(System.Collections.Generic.List<T>)",
        "System.Runtime.Serialization.DataContractAttribute.DataContractAttribute()",
        "System.Security.AllowPartiallyTrustedCallersAttribute.AllowPartiallyTrustedCallersAttribute()",

        "System.SerializableAttribute.SerializableAttribute()",

        "System.Threading.ThreadLocal<T>.ThreadLocal(System.Func<T>)",
        "System.UIntPtr.UIntPtr(uint)",


        "System.Collections.Generic.ICollection<T>.Count.get",
        "System.Collections.Generic.IDictionary<TKey, TValue>.Keys.get",
        "System.Collections.Generic.IDictionary<TKey, TValue>.Values.get",
        "System.Collections.Generic.IEnumerable<T>.GetEnumerator()",
        "System.Collections.Generic.IEnumerator<T>.Current.get",
        "System.Collections.Generic.KeyValuePair<TKey, TValue>.KeyValuePair(TKey, TValue)",
        "System.Collections.Generic.KeyValuePair<TKey, TValue>.Key.get",
        "System.Collections.Generic.KeyValuePair<TKey, TValue>.Value.get",
        "System.ComponentModel.DataAnnotations.RequiredAttribute.RequiredAttribute()",
        "System.ComponentModel.DataAnnotations.StringLengthAttribute.StringLengthAttribute(int)",
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
        "System.Enum.TryParse<TEnum>(string, out TEnum)",
        "System.Environment.NewLine.get",




        "System.IEquatable<T>.Equals(T)",

        "System.Linq.Enumerable.Average",
        "System.Linq.Enumerable.Distinct<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.ElementAt<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)",
        "System.Linq.Enumerable.First<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Last<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Max",
        "System.Linq.Enumerable.Min",
        "System.Linq.Enumerable.OrderByDescending",
        "System.Linq.Enumerable.Reverse<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Single<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Linq.Enumerable.Skip<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)",
        "System.Linq.Enumerable.Take<TSource>(System.Collections.Generic.IEnumerable<TSource>, int)",
        "System.Linq.Enumerable.ThenBy",
        "System.Linq.Enumerable.Zip",
        "System.Math.Sign(decimal)",
        "System.Math.Truncate(double)",
        "System.Net.Http.HttpRequestMessage.HttpRequestMessage(System.Net.Http.HttpMethod, string)",
        "System.Net.Http.HttpResponseMessage.IsSuccessStatusCode.get",
        "System.Net.Http.StringContent.StringContent(string, System.Text.Encoding, string)",
        "System.Net.IPEndPoint.IPEndPoint(System.Net.IPAddress, int)",
        "System.ObjectDisposedException.ObjectDisposedException(string)",
        "System.OperatingSystem.Platform.get",
        "System.Reflection.MemberInfo.GetCustomAttributes(bool)",
        "System.Reflection.PropertyInfo.PropertyType.get",
        "System.Runtime.InteropServices.Marshal.SizeOf<T>()",
        "string.Contains(string)",
        "string.EndsWith(string)",
        "string.IndexOf(char)",
        "string.Insert(int, string)",
        "string.Join(string, System.Collections.Generic.IEnumerable<string>)",
        "string.PadLeft(int)",
        "string.Remove(int)",
        "string.StartsWith(string)",
        "System.Text.Encoding.ASCII.get",
        "System.Text.StringBuilder.Length.get",
        "System.Threading.CancellationToken.None.get",
        "System.Threading.Interlocked.Read(ref long)",
        "System.TimeSpan.CompareTo(System.TimeSpan)",
        "System.TimeSpan.FromDays(double)",
        "System.Uri.ToString()",


        "System.AggregateException.AggregateException(System.Collections.Generic.IEnumerable<System.Exception>)",
        "System.AggregateException.Flatten()",
        "System.ArgumentException.ArgumentException(string, string)",
        "System.Array.AsReadOnly<T>(T[])",
        "System.Array.BinarySearch(System.Array, object)",
        "System.Array.Find<T>(T[], System.Predicate<T>)",
        "System.Array.FindIndex<T>(T[], System.Predicate<T>)",
        "bool.CompareTo(bool)",
        "bool.TryParse(string, out bool)",
        "byte.Parse(string)",
        "byte.TryParse(string, out byte)",
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
        "System.Collections.Generic.List<T>.BinarySearch(T)",
        "System.Collections.Generic.List<T>.Capacity.get",
        "System.Collections.Generic.List<T>.ConvertAll<TOutput>(System.Converter<T, TOutput>)",

        "System.Collections.Generic.List<T>.FindAll(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.FindIndex(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.FindLast(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.IndexOf(T)",
        "System.Collections.Generic.List<T>.LastIndexOf(T)",
        "System.Collections.Generic.List<T>.ToArray()",

        "System.Collections.Generic.Queue<T>.Contains(T)",
        "System.Collections.Generic.Queue<T>.ToArray()",
        "System.Collections.Generic.Stack<T>.Contains(T)",
        "System.Collections.Generic.Stack<T>.ToArray()",
        "System.Convert.FromBase64String(string)",
        "System.Convert.ToBase64String(byte[])",
        "System.Convert.ToDouble(object)",
        "System.Convert.ToInt32(object)",
        "System.Convert.ToString(object)",
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
        "System.DateTime.ToLongDateString()",
        "System.DateTime.ToLongTimeString()",


        "System.Array.GetLength(int)",
        "System.Array.IndexOf<T>(T[], T)",
        "System.Array.LastIndexOf<T>(T[], T)",
        "System.Attribute.IsDefined(System.Reflection.MemberInfo, System.Type)",
        "System.Buffers.ReadOnlySequence<T>.End.get",
        "System.Buffers.ReadOnlySequence<T>.IsEmpty.get",
        "System.Buffers.ReadOnlySequence<T>.Length.get",
        "System.Buffers.ReadOnlySequence<T>.Start.get",
        "char.ConvertFromUtf32(int)",
        "char.ConvertToUtf32(char, char)",
        "System.Collections.Generic.Dictionary<TKey, TValue>.Values.get",
        "System.Collections.Generic.EqualityComparer<T>.Equals(T, T)",
        "System.Collections.Generic.EqualityComparer<T>.GetHashCode(T)",
        "System.Collections.Generic.ICollection<T>.Contains(T)",
        "System.Collections.Generic.IList<T>.IndexOf(T)",
        "System.Collections.Generic.List<T>.Contains(T)",
        "System.Collections.Generic.Queue<T>.TryPeek(out T)",
        "System.Collections.Generic.SortedSet<T>.GetViewBetween(T, T)",
        "System.ComponentModel.DataAnnotations.EmailAddressAttribute.EmailAddressAttribute()",
        "System.ComponentModel.DataAnnotations.RegularExpressionAttribute.RegularExpressionAttribute(string)",
        "System.ComponentModel.TypeDescriptor.GetConverter(System.Type)",
        "System.Convert.FromHexString(string)",
        "System.Convert.ToHexString(byte[])",
        "System.Convert.ToInt16(object)",
        "System.Convert.ToSingle(object)",
        "System.DateTime.Parse(string)",
        "System.DateTime.ParseExact(string, string, System.IFormatProvider)",
        "System.DateTimeOffset.Parse(string)",
        "System.DateTimeOffset.ParseExact(string, string, System.IFormatProvider)",
        "decimal.Negate(decimal)",
        "decimal.Parse(string)",
        "decimal.TryParse(string, out decimal)",
        "System.Diagnostics.ActivitySource.ActivitySource(string, string)",
        "System.Diagnostics.DiagnosticListener.DiagnosticListener(string)",
        "System.Diagnostics.FileVersionInfo.FileVersion.get",
        "System.Diagnostics.Process.Id.get",
        "System.Diagnostics.Process.StartInfo.get",
        "double.PositiveInfinity.get",
        "System.FileNotFoundException.FileNotFoundException(string)",
        "System.FormattableString.Format.get",
        "System.FormattableString.ToString(System.IFormatProvider)",
        "System.HashCode.HashCode()",
        "System.HashCode.ToHashCode()",
        "System.Index.End.get",
        "System.Index.Start.get",
        "long.Parse(string)",
        "long.TryParse(string, out long)",
        "System.InvalidOperationException.InvalidOperationException(string)",
        "System.IO.DirectoryInfo.Name.get",
        "System.IO.DirectoryInfo.Parent.get",
        "System.IO.FileInfo.DirectoryName.get",
        "System.IO.FileInfo.Extension.get",
        "System.IO.FileInfo.Name.get",

        "System.Linq.Enumerable.TakeWhile<TSource>(System.Collections.Generic.IEnumerable<TSource>, System.Func<TSource, bool>)",
        "System.Math.Abs(int)",
        "System.Math.Ceiling(decimal)",
        "System.Net.IPAddress.Any.get",
        "System.Net.IPAddress.Parse(System.ReadOnlySpan<char>)",
        "System.NotSupportedException.NotSupportedException(string)",
        "object.MemberwiseClone()",
        "System.Reflection.MethodBase.IsStatic.get",
        "System.Reflection.TypeInfo.IsValueType.get",
        "System.Runtime.InteropServices.MemoryMarshal.AsBytes<T>(System.Span<T>)",
        "string.IsNullOrWhiteSpace(System.ReadOnlySpan<char>)",
        "System.TimeSpan.Zero.get",


        "System.Xml.Linq.XDocument.Parse(string)",
        "System.Xml.Linq.XElement.Attribute(System.Xml.Linq.XName)",
        "System.Xml.Linq.XElement.Descendants()",
        "System.Xml.Linq.XElement.Elements()",
        "System.Xml.Linq.XElement.Value.get",
        "System.Xml.Linq.XAttribute.Value.get",


        "System.Text.RegularExpressions.Regex.IsMatch(string, string)",
        "System.Linq.Enumerable.ToHashSet<TSource>(System.Collections.Generic.IEnumerable<TSource>)",
        "System.Collections.Generic.List<T>.Find(System.Predicate<T>)",
        "System.Collections.Generic.List<T>.get_Count()",
        "System.String.StartsWith(System.String)",


        "System.Math.Abs",
































        "object.Equals(object)",
        "object.GetHashCode()",
        "object.GetType()",
        "object.ReferenceEquals(object, object)",
        "object.ToString()",


        "System.ReadOnlySpan<T>.Length.get",
        "System.ReadOnlySpan<T>.IsEmpty.get",
        "System.ReadOnlySpan<T>.ToArray()",
        "System.ReadOnlySpan<T>.Slice(int, int)",
        "System.Span<T>.Length.get",
        "System.Span<T>.IsEmpty.get",


        "string.Clone()",
        "string.CompareTo(string)",
        "string.Contains(string)",
        "string.EndsWith(string)",
        "string.Equals(string)",
        "string.Equals(string, string)",
        "string.Equals(string, System.StringComparison)",
        "string.GetHashCode()",
        "string.IndexOf(char)",
        "string.IndexOf(string)",
        "string.IsNullOrEmpty(string)",
        "string.IsNullOrWhiteSpace(string)",
        "string.Length.get",
        "string.StartsWith(string)",
        "string.Substring(int)",
        "string.Substring(int, int)",
        "string.ToCharArray()",
        "string.ToLower()",
        "string.ToLowerInvariant()",
        "string.ToString()",
        "string.ToUpper()",
        "string.ToUpperInvariant()",
        "string.Trim()",
        "string.TrimEnd()",
        "string.TrimStart()",






        "System.Text.Encoding.UTF8.get",
        "System.Text.Encoding.GetString(byte[])",
        // Exception guard helpers considered pure (diverging without side effects)
        "System.ArgumentNullException.ThrowIfNull(object)",
        "System.ArgumentNullException.ThrowIfNull(object, string)",
    };
}