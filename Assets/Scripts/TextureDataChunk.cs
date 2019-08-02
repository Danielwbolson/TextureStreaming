using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Struct to receive image data and allow capability to sort by imageIndex
public struct TextureDataChunk {

    public short totalChunks;
    public short chunkIndex;
    public int bufferLength;
    public byte[] dataBuffer;
    public int lastPixelIndex;

};
