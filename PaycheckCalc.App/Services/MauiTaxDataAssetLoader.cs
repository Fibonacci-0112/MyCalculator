using PaycheckCalc.Core.Data;

namespace PaycheckCalc.App.Services;

/// <summary>
/// MAUI implementation of <see cref="ITaxDataAssetLoader"/>. Reads JSON tax
/// tables shipped as <c>MauiAsset</c> entries via
/// <see cref="FileSystem.OpenAppPackageFileAsync"/>.
/// </summary>
public sealed class MauiTaxDataAssetLoader : ITaxDataAssetLoader
{
    public async Task<string> ReadAllTextAsync(string assetName)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(assetName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
