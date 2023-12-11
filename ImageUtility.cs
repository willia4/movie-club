using System.Collections.Immutable;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace zinfandel_movie_club;
using SixLabors.ImageSharp;

public static class ImageUtility
{
    public static Task<Result<(Image, IImageFormat), Exception>> LoadImageFromBytes(ImmutableArray<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var img = Image.Load(bytes.AsSpan(), format: out var format);
            return Task.FromResult(Result<(Image, IImageFormat), Exception>.Ok((img, format)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<(Image, IImageFormat), Exception>.Error(ex));
        }
    }

    public static Task<Result<(Image, IImageFormat), Exception>> LoadImageFromBytes(MemoryStream bytes, CancellationToken cancellationToken)
    {
        return LoadImageFromBytes(bytes.ToArray().ToImmutableArray(), cancellationToken);
    }
    
    public static async Task<Result<(Image, IImageFormat), Exception>> LoadImageFromBytes(Stream bytes, CancellationToken cancellationToken)
    {
        try
        {
            using var ms = new MemoryStream();
            await bytes.CopyToAsync(ms, cancellationToken);
            return await LoadImageFromBytes(ms.ToArray().ToImmutableArray(), cancellationToken);
        }
        catch (TaskCanceledException c)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<(Image, IImageFormat), Exception>.Error(ex);
        }
    }

    public static ImmutableArray<byte> ToPngImmutableByteArray(this Image image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, PngFormat.Instance);
        return ms.ToArray().ToImmutableArray();
    }
    
    public static ImmutableArray<byte> ToPngImmutableByteArray(this Image image, int newWidth)
    {
        var (currentWidth, _) = image.Size();
        
        // ResizeToWidth will always clone the image; since we only care about the bytes that we get out of it, 
        // the cloning or lack of cloning is immaterial to us. So skip that step if the widths already match
        var sizedImage = currentWidth == newWidth ? image : image.ResizeToWidth(newWidth);

        return sizedImage.ToPngImmutableByteArray();
    }

    public static Image Clone(this Image image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, SixLabors.ImageSharp.Formats.Bmp.BmpFormat.Instance);
        ms.Seek(0, SeekOrigin.Begin);
        
        return Image.Load(ms);
    }

    public static Image ResizeToWidth(this Image image, int newWidth)
    {
        var (originalWidth, originalHeight) = image.Size();
        var aspectRatio = (double) originalWidth / (double) originalHeight;

        var newHeight = (int) Math.Floor(newWidth / aspectRatio);
        
        var dst = image.Clone();

        if (originalWidth != newWidth)
        {
            dst.Mutate(x => { x.Resize(width: newWidth, height: newHeight, sampler: KnownResamplers.Lanczos3); });
        }

        return dst;
    }

    public static void Deconstruct(this Size size, out int Width, out int Height)
    {
        Width = size.Width;
        Height = size.Height;
    }
}