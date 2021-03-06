﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AGSUnpackerSharp.Game
{
  public class AGSAudioClip
  {
    public Int32 id;
    public string scriptname;
    public string filename;
    public byte type_bundling;
    public byte type;
    public byte type_file;
    public byte repeat_default;
    public Int16 priority_default;
    public Int16 volume_default;
    public Int32 reserved1;

    public AGSAudioClip()
    {
      id = 0;
      scriptname = string.Empty;
      filename = string.Empty;
      type_bundling = 0;
      type = 0;
      type_file = 0;
      repeat_default = 0;
      priority_default = 0;
      volume_default = 0;
      reserved1 = 0;
    }

    public void LoadFromStream(AGSAlignedStream ar)
    {
      id = ar.ReadInt32();
      scriptname = ar.ReadFixedString(30);
      filename = ar.ReadFixedString(15);
      type_bundling = ar.ReadByte();
      type = ar.ReadByte();
      type_file = ar.ReadByte();
      repeat_default = ar.ReadByte();
      priority_default = ar.ReadInt16();
      volume_default = ar.ReadInt16();
      reserved1 = ar.ReadInt32();
    }
  }
}
