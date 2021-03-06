﻿using System;
using System.IO;
using AGSUnpackerSharp.Utils.Encryption;

namespace AGSUnpackerSharp.Extensions
{
  public static class BinaryWriterExtension
  {
    public static void WriteCString(this BinaryWriter writer, string text)
    {
      WriteCString(writer, text, AGSStringUtils.MaxCStringLength);
    }

    public static void WriteCString(this BinaryWriter writer, string text, int lengthMax)
    {
      byte[] buffer = new byte[lengthMax];
      int length = 0;

      for (int i = 0; i < text.Length; ++i)
      {
        if (i == lengthMax)
          break;

        buffer[i] = (byte)text[i];
        ++length;
      }

      writer.Write((byte[])buffer, 0, length);

      //NOTE(adm244): if string length exeeds maxLength it shouldn't be null-terminated
      if (length < lengthMax)
        writer.Write((byte)0);
    }

    public static void WriteEncryptedCString(this BinaryWriter writer, string text)
    {
      byte[] buffer = AGSEncryption.EncryptAvis(text);
      writer.Write((Int32)buffer.Length);
      writer.Write((byte[])buffer);
    }

    public static void WriteFixedString(this BinaryWriter writer, string text, int length)
    {
      char[] buffer = new char[length + 1];

      for (int i = 0; i < text.Length; ++i)
        buffer[i] = text[i];

      //NOTE(adm244): don't trust microsoft to have it initialized to 0
      buffer[length] = (char)0;

      writer.Write((char[])buffer);
    }

    public static void WritePrefixedString32(this BinaryWriter writer, string text)
    {
      writer.Write((Int32)text.Length);

      char[] buffer = text.ToCharArray();
      writer.Write((char[])buffer);
    }

    public static void WriteArrayInt32(this BinaryWriter writer, int[] values)
    {
      for (int i = 0; i < values.Length; ++i)
        writer.Write(values[i]);
    }
  }
}
