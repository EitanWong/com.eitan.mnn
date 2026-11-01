using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class VisionPlayerData
    {
        internal static MNNRgbImage Read(string path)
        {
            var texture = new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                Assert.True(texture.LoadImage(File.ReadAllBytes(path)));
                var colors=texture.GetPixels32();
                var pixels=new byte[colors.Length*3];
                for(int y=0;y<texture.height;++y)
                    for(int x=0;x<texture.width;++x)
                    {
                        var color=colors[(texture.height-y-1)*texture.width+x];
                        int offset=(y*texture.width+x)*3;
                        pixels[offset]=color.r;pixels[offset+1]=color.g;pixels[offset+2]=color.b;
                    }
                return new MNNRgbImage(pixels,texture.width,texture.height);
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
