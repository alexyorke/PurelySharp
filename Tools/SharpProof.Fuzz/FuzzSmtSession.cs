using SharpProof.Host;
using SharpProof.Smt;
using SharpProof.Verify;

namespace SharpProof.Fuzz;

internal sealed class FuzzSmtSession : IDisposable
{
    private readonly IrSmtBackend _backend;

    private FuzzSmtSession(IrSmtBackend backend)
    {
        _backend = backend;
        Kernel = new ProofKernel(backend);
    }

    internal ProofKernel Kernel
    {
        get;
    }

    internal static FuzzSmtSession Create()
    {
        ContainerNativeLibrary.InstallZ3ResolverRequired(
            typeof(Microsoft.Z3.Context).Assembly);
        var backend = new IrSmtBackend();
        try
        {
            return new FuzzSmtSession(backend);
        }
        catch
        {
            backend.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _backend.Dispose();
    }
}
