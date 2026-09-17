using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
namespace AssetCopilot;

// APIs shared by the Framework and Core hosts. No Rhino runtime setting is changed.
public static class Compat
{
    public static bool IsPathFullyQualified(string path)=>!string.IsNullOrWhiteSpace(path)&&((path.Length>=3&&char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/'))||path.StartsWith(@"\\",StringComparison.Ordinal));
    public static string TrimEndingDirectorySeparator(string path)=>path.Length>(Path.GetPathRoot(path)?.Length??0)?path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar):path;
    public static HttpRequestException HttpError(string message,System.Net.HttpStatusCode status)
    {
#if NETFRAMEWORK
        var error=new HttpRequestException(message);error.Data["StatusCode"]=status;return error;
#else
        return new HttpRequestException(message,null,status);
#endif
    }
    public static double Clamp(double value,double min,double max)=>Math.Min(Math.Max(value,min),max);
    public static int Clamp(int value,int min,int max)=>Math.Min(Math.Max(value,min),max);
    public static bool IsFinite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    public static string HashHex(byte[] bytes){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    public static Task<byte[]> ReadAllBytesAsync(string path,CancellationToken ct)=>Task.Run(()=>{ct.ThrowIfCancellationRequested();return File.ReadAllBytes(path);},ct);
    public static Task WriteAllBytesAsync(string path,byte[] bytes,CancellationToken ct)=>Task.Run(()=>{ct.ThrowIfCancellationRequested();File.WriteAllBytes(path,bytes);},ct);
    public static void ReplaceFile(string source,string destination){if(File.Exists(destination))File.Replace(source,destination,null);else File.Move(source,destination);}
    public static string GetRelativePath(string root,string file)
    {
        var start=new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar);
        return Uri.UnescapeDataString(start.MakeRelativeUri(new Uri(Path.GetFullPath(file))).ToString()).Replace('/',Path.DirectorySeparatorChar);
    }
    // Windows CommandLineToArgvW quoting. Preserve spaces, Unicode, embedded quotes and trailing slashes.
    public static string QuoteArgument(string value)
    {
        var b=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
        b.Append('\\',slashes*2);b.Append('"');return b.ToString();
    }
    public static void SetArguments(ProcessStartInfo start,params string[] args)=>start.Arguments=string.Join(" ",args.Select(QuoteArgument));
#if NETFRAMEWORK
    public static async Task WaitAsync(this Task task,TimeSpan timeout)
    {
        using var stop=new CancellationTokenSource();
        if(await Task.WhenAny(task,Task.Delay(timeout,stop.Token)).ConfigureAwait(false)!=task)throw new TimeoutException();
        stop.Cancel();await task.ConfigureAwait(false);
    }
    public static async Task<T> WaitAsync<T>(this Task<T> task,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();var cancelled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration=ct.Register(()=>cancelled.TrySetResult(true));
        if(await Task.WhenAny(task,cancelled.Task).ConfigureAwait(false)!=task)throw new OperationCanceledException(ct);
        return await task.ConfigureAwait(false);
    }
    public static async Task WaitForExitAsync(this Process process,CancellationToken ct)
    {
        while(!process.HasExited)await Task.Delay(50,ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }
    public static Task<string> ReadAsStringAsync(this HttpContent content,CancellationToken ct)=>content.ReadAsStringAsync().WaitAsync(ct);
    public static Task<Stream> ReadAsStreamAsync(this HttpContent content,CancellationToken ct)=>content.ReadAsStreamAsync().WaitAsync(ct);
#endif
}
