using System.Collections.Immutable;
using SixLabors.ImageSharp;

namespace zinfandel_movie_club.Data;


public abstract class ImageSize : IEquatable<ImageSize>
{
    public abstract int? MaxWidth { get; }
    public abstract string Name { get; }
    public virtual string FileName => MaxWidth.HasValue ? $"{MaxWidth.Value}" : Name.ToLowerInvariant();

    public static ImageSize SizeOriginal { get; } = new ImageSizeOriginal();
    public static ImageSize Size1024 { get; } = new ImageSize1024();
    public static ImageSize Size512 { get; } = new ImageSize512();
    public static ImageSize Size256 { get; } = new ImageSize256();
    public static ImageSize Size128 { get; } = new ImageSize128();

    public static IReadOnlyCollection<ImageSize> AllImageSizes => ImmutableList<ImageSize>.Empty.AddRange(
        new ImageSize[] {SizeOriginal, Size1024, Size512, Size256, Size128});

    public SixLabors.ImageSharp.Image SizedImage(Image image)
    {
        return MaxWidth.HasValue ? image.ResizeToWidth(MaxWidth.Value) : image.Clone();
    }

    public ImmutableArray<byte> ImageToSizedPng(Image image)
    {
        return MaxWidth.HasValue ? image.ToPngImmutableByteArray(MaxWidth.Value) : image.ToPngImmutableByteArray();
    }

    public IReadOnlyCollection<(ImageSize, Image)> SizedImages(Image image)
    {
        return AllImageSizes.Select(size => (size, size.SizedImage(image))).ToImmutableList();
    }
    
    public IReadOnlyCollection<(ImageSize, ImmutableArray<byte>)> SizedPngs(Image image)
    {
        return AllImageSizes.Select(size => (size, size.ImageToSizedPng(image))).ToImmutableList();
    }
    
    public abstract bool Equals(ImageSize? other);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ImageSize)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.GetType(), Name);
    }

    public static bool operator ==(ImageSize? left, ImageSize? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ImageSize? left, ImageSize? right)
    {
        return !Equals(left, right);
    }
}

#region "Image Sizes"
public class ImageSizeOriginal : ImageSize
{
    public override int? MaxWidth => null;

    public override string Name => "Original";

    public override bool Equals(ImageSize? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is ImageSizeOriginal;
    }
}

public abstract class ImageSizeWidth : ImageSize
{
    protected readonly int _width;
    public override int? MaxWidth => _width;
    public override string Name => $"{_width}";

    protected ImageSizeWidth(int width)
    {
        _width = width;
    }
    
    public override bool Equals(ImageSize? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is ImageSizeWidth w && w._width == this._width;
    }
}

public class ImageSize1024 : ImageSizeWidth { public ImageSize1024() : base(1024) { } }
public class ImageSize512 : ImageSizeWidth { public ImageSize512() : base(512) { } }
public class ImageSize256 : ImageSizeWidth { public ImageSize256() : base(256) { } }
public class ImageSize128 : ImageSizeWidth { public ImageSize128() : base(128) { } }
#endregion
