#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Poster;

#region DragImage
[StructLayout(LayoutKind.Sequential)]
public struct Win32Point
{
	public int x;
	public int y;
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32Size
{
	public int cx;
	public int cy;
}

[StructLayout(LayoutKind.Sequential)]
public struct ShDragImage
{
	public Win32Size sizeDragImage;
	public Win32Point ptOffset;
	public IntPtr hbmpDragImage;
	public int crColorKey;
}
#endregion

static partial class Helpers
{
	/// <seealso cref="https://stackoverflow.com/questions/8442085/receiving-an-image-dragged-from-web-page-to-wpf-window"/>
	public static BitmapSource GetDragImage(this MemoryStream imageStream)
	{
		imageStream.Seek(0, SeekOrigin.Begin);
		BinaryReader br = new(imageStream);
		ShDragImage shDragImage;
		shDragImage.sizeDragImage.cx = br.ReadInt32();
		shDragImage.sizeDragImage.cy = br.ReadInt32();
		shDragImage.ptOffset.x = br.ReadInt32();
		shDragImage.ptOffset.y = br.ReadInt32();
		shDragImage.hbmpDragImage = new IntPtr(br.ReadInt32()); // I do not know what this is for!
		shDragImage.crColorKey = br.ReadInt32();
		int stride = shDragImage.sizeDragImage.cx * 4;
		var imageData = new byte[stride * shDragImage.sizeDragImage.cy];
		// We must read the image data as a loop, so it's in a flipped format
		for (int i = (shDragImage.sizeDragImage.cy - 1) * stride; i >= 0; i -= stride)
		{
			br.Read(imageData, i, stride);
		}
		return BitmapSource.Create(
			shDragImage.sizeDragImage.cx, shDragImage.sizeDragImage.cy,
			96, 96, PixelFormats.Bgra32, palette: null, pixels: imageData, stride);
	}
}
