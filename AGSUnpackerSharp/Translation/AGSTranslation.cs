﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AGSUnpackerSharp.Extensions;

namespace AGSUnpackerSharp.Translation
{
  public class AGSTranslation
  {
    private static readonly string TRA_SIGNATURE = "AGSTranslation\x0";

    private List<string> OriginalLines = new List<string>();
    private List<string> TranslatedLines = new List<string>();

    public uint GameID { get; private set; }
    public string GameName { get; private set; }

    public AGSTranslation()
    {
    }

    public AGSTranslation(IEnumerable<string> originalLines, IEnumerable<string> translatedLines)
    {
      OriginalLines.AddRange(originalLines);
      TranslatedLines.AddRange(translatedLines);
    }

    public bool Add(string original, string translation)
    {
      if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translation))
        return false;

      OriginalLines.Add(original);
      TranslatedLines.Add(translation);

      return true;
    }

    public void Compile(string filepath)
    {
      Compile(filepath, GameID, GameName);
    }

    public void Compile(string filepath, uint gameID, string gameName)
    {
      if (OriginalLines.Count != TranslatedLines.Count)
      {
        Trace.Assert(false, "AGSTranslation::Compile: Original and Tranlated lines count do not match!");
        return;
      }

      GameID = gameID;
      GameName = gameName;

      using (FileStream stream = new FileStream(filepath, FileMode.Create, FileAccess.Write))
      {
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.GetEncoding(1252)))
        {
          writer.WriteFixedString(TRA_SIGNATURE, TRA_SIGNATURE.Length);

          WriteBlock(writer, BlockType.Header);
          WriteBlock(writer, BlockType.Content);

          //TODO(adm244): implement settings block
          //WriteBlock(writer, BlockType.Settings);

          WriteBlock(writer, BlockType.End);
        }
      }
    }

    private void WriteBlock(BinaryWriter writer, BlockType type)
    {
      writer.Write((UInt32)type);
      writer.Write((UInt32)0xDEADBEAF);

      long blockStart = writer.BaseStream.Position;

      switch (type)
      {
        case BlockType.Header:
          {
            writer.Write((UInt32)GameID);
            writer.WriteEncryptedCString(GameName);
          } break;

        case BlockType.Content:
          {
            for (int i = 0; i < OriginalLines.Count; ++i)
            {
              //NOTE(adm244): skip lines that have no translation
              if (string.IsNullOrEmpty(TranslatedLines[i]))
                continue;

              writer.WriteEncryptedCString(OriginalLines[i]);
              writer.WriteEncryptedCString(TranslatedLines[i]);
            }

            //NOTE(adm244): write empty strings so parser knows this block has ended
            // no idea why they just didn't use a block size value...
            writer.WriteEncryptedCString("");
            writer.WriteEncryptedCString("");
          } break;

        case BlockType.Settings:
          throw new NotImplementedException("AGSTranslation: Setting block is not implemented!");

        case BlockType.End:
          // do nothing
          break;

        default:
          throw new NotImplementedException("AGSTranslation: Block type is not implemented!");
      }

      long blockEnd = writer.BaseStream.Position;

      //NOTE(adm244): go back and write a block size
      UInt32 blockSize = (UInt32)(blockEnd - blockStart);
      writer.BaseStream.Position = (blockStart - sizeof(UInt32));
      writer.Write((UInt32)blockSize);

      writer.BaseStream.Position = blockEnd;
    }

    public void Decompile(string filepath)
    {
      using (FileStream stream = new FileStream(filepath, FileMode.Open))
      {
        using (BinaryReader reader = new BinaryReader(stream, Encoding.GetEncoding(1252)))
        {
          string signature = reader.ReadFixedString(15);
          if (signature != TRA_SIGNATURE)
          {
            Debug.Assert(false, "Invalid TRA signature!");
            return;
          }

          for (; ; )
          {
            Int32 blockType = reader.ReadInt32();
            Int32 blockSize = reader.ReadInt32();

            if (blockType == (int)BlockType.Content)
            {
              for (; ; )
              {
                string original = reader.ReadEncryptedCString();
                string translation = reader.ReadEncryptedCString();

                if ((original.Length < 1) && (translation.Length < 1))
                  break;

                OriginalLines.Add(original);
                TranslatedLines.Add(translation);
              }
            }
            else if (blockType == (int)BlockType.Header)
            {
              GameID = reader.ReadUInt32();
              GameName = reader.ReadEncryptedCString();
            }
            else if (blockType == (int)BlockType.Settings)
            {
              //TODO(adm244): read settings
              break;
            }
            else
            {
              Debug.Assert(false, "Unknown block type encountered!");
              break;
            }
          }
        }
      }
    }

    public static AGSTranslation ReadSourceFile(string filename)
    {
      AGSTranslation translation = new AGSTranslation();

      using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
      {
        using (StreamReader reader = new StreamReader(stream, Encoding.GetEncoding(1252)))
        {
          ReadState state = ReadState.Original;

          while (!reader.EndOfStream)
          {
            string line = reader.ReadLine();
            if (line.StartsWith("//"))
              continue;

            //TODO(adm244): read settings

            switch (state)
            {
              case ReadState.Original:
                {
                  translation.OriginalLines.Add(line);
                  state = ReadState.Translation;
                }
                break;

              case ReadState.Translation:
                {
                  translation.TranslatedLines.Add(line);
                  state = ReadState.Original;
                }
                break;

              default:
                throw new InvalidDataException();
            }
          }
        }
      }

      return translation;
    }

    private enum BlockType
    {
      Invalid = 0,
      End = -1,
      Content = 1,
      Header = 2,
      Settings = 3,
    }

    private enum ReadState
    {
      Invalid = 0,
      Original,
      Translation,
    }
  }
}
