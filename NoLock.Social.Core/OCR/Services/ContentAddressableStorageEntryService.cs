using System.Security;
using NoLock.Social.Core.Cryptography.Interfaces;
using NoLock.Social.Core.Storage;

namespace NoLock.Social.Core.OCR.Services;

public sealed class ContentAddressableStorageEntryService<T>(
    IContentAddressableStorage<T> bytesCas,
    IContentAddressableStorage<ContentMetadata> metadataCas,
    IContentAddressableStorage<SignedTarget> signedTargetCas,
    ISessionStateService sessionState,
    ISigningService signingService
) : IContentAddressableStorageEntryService<T>
{
    public async ValueTask<string> SubmitAsync(T value, string documentType)
    {
        if (!sessionState.IsUnlocked || sessionState.CurrentSession is null)
            throw new SecurityException();
        
        var session = sessionState.CurrentSession;
        var keyPair = new Ed25519KeyPair
        {
            PublicKey = session.PublicKey,
            PrivateKey = session.PrivateKeyBuffer?.Data ?? throw new SecurityException()
        };
        
        var valueHash = await bytesCas.StoreAsync(value);
        var valueMetadata = new ContentMetadata
        {
            References = [
                new ContentReference(valueHash, documentType)
            ],
        };
        var metadataHash = await metadataCas.StoreAsync(valueMetadata);
        var signed = await signingService.SignAsync(metadataHash, keyPair);
        var signedHash = await signedTargetCas.StoreAsync(signed);
        
        return signedHash;
    }
}