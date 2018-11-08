﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace AGSUnpackerSharp
{
  public class AGSTextParser
  {
    private readonly string CLIB_HEAD_SIGNATURE = "CLIB\x1a";
    private readonly string CLIB_TAIL_SIGNATURE = "CLIB\x1\x2\x3\x4SIGE";

    private readonly string DTA_SIGNATURE = "Adventure Creator Game File v2";

    private readonly string SCRIPT_SIGNATURE = "SCOM";

    private readonly Int32 EncryptionRandSeed = 9338638;

    public string[] UnpackAGSAssetFiles(string agsfile)
    {
      FileStream fs = new FileStream(agsfile, FileMode.Open, FileAccess.Read, FileShare.Read);
      BinaryReader r = new BinaryReader(fs, Encoding.ASCII);

      Console.Write("Parsing {0}...", agsfile);
      AGSAssetInfo[] assetInfos = ParseAGSAssetInfos(r);
      Console.WriteLine("Done!");

      Console.WriteLine("Extracting data files...");
      string[] filenames = ExtractAGSAssetFiles(r, assetInfos, "Data");
      Console.WriteLine("Done!");

      r.Close();

      return filenames;
    }

    private AGSAssetInfo[] ParseAGSAssetInfos(BinaryReader r)
    {
      // verify tail signature
      r.BaseStream.Seek(-CLIB_TAIL_SIGNATURE.Length, SeekOrigin.End);
      Debug.Assert(r.BaseStream.Position == 0xD160CB);

      char[] tail_sig = r.ReadChars(CLIB_TAIL_SIGNATURE.Length);
      string tail_sig_string = new string(tail_sig);
      Debug.Assert(CLIB_TAIL_SIGNATURE == tail_sig_string);

      // get clib offset
      r.BaseStream.Seek(-(CLIB_TAIL_SIGNATURE.Length + 4), SeekOrigin.End);
      Debug.Assert(r.BaseStream.Position == 0xD160C7);

      UInt32 clib_offset = r.ReadUInt32();
      Debug.Assert(clib_offset == 0x20EC00);

      r.BaseStream.Seek(clib_offset, SeekOrigin.Begin);
      Debug.Assert(r.BaseStream.Position == clib_offset);

      // verify clib signature
      char[] head_sig = r.ReadChars(CLIB_HEAD_SIGNATURE.Length);
      string head_sig_string = new string(head_sig);
      Debug.Assert(CLIB_HEAD_SIGNATURE == head_sig_string);

      // parse clib
      byte clib_version = r.ReadByte();
      Debug.Assert(clib_version == 0x15);

      byte asset_index = r.ReadByte();
      Debug.Assert(asset_index == 0);

      Int32 rand_val = r.ReadInt32() + EncryptionRandSeed;

      AGSEncoder encoder = new AGSEncoder(rand_val);
      Int32 files_count = encoder.ReadInt32(r);
      Debug.Assert(files_count == 0x01);

      for (int i = 0; i < files_count; ++i)
      {
        string lib_filename = encoder.ReadString(r);
        Debug.Assert(lib_filename == "Uploaded.exe");
      }

      Int32 asset_count = encoder.ReadInt32(r);
      AGSAssetInfo[] assetInfos = new AGSAssetInfo[asset_count];
      for (int i = 0; i < asset_count; ++i)
      {
        assetInfos[i].Filename = encoder.ReadString(r);
      }
      for (int i = 0; i < asset_count; ++i)
      {
        assetInfos[i].Offset = encoder.ReadInt32(r) + (Int32)clib_offset;
      }
      for (int i = 0; i < asset_count; ++i)
      {
        assetInfos[i].Size = encoder.ReadInt32(r);
      }
      for (int i = 0; i < asset_count; ++i)
      {
        assetInfos[i].UId = encoder.ReadInt8(r);
      }

      return assetInfos;
    }

    private string[] ExtractAGSAssetFiles(BinaryReader r, AGSAssetInfo[] assetInfos, string targetpath)
    {
      string[] filenames = new string[assetInfos.Length];

      string dirpath = Path.Combine(Environment.CurrentDirectory, targetpath);
      if (!Directory.Exists(dirpath))
      {
        Directory.CreateDirectory(dirpath);
      }

      for (int i = 0; i < assetInfos.Length; ++i)
      {
        string filepath = Path.Combine(dirpath, assetInfos[i].Filename);
        filenames[i] = filepath;

        FileStream fs = new FileStream(filepath, FileMode.Create);
        BinaryWriter w = new BinaryWriter(fs, Encoding.ASCII);

        r.BaseStream.Seek(assetInfos[i].Offset, SeekOrigin.Begin);

        Console.Write("\tExtracting {0}...", assetInfos[i].Filename);

        // 1048576 bytes = 1 mb
        byte[] buffer = new byte[1048576];
        int bytesRead = 0;
        while (bytesRead < assetInfos[i].Size)
        {
          int bytesLeftToRead = assetInfos[i].Size - bytesRead;
          int bytesToRead = Math.Min(buffer.Length, bytesLeftToRead);
          buffer = r.ReadBytes(bytesToRead);
          w.Write(buffer);

          bytesRead += bytesToRead;
        }

        w.Close();

        Console.WriteLine(" Done!");
      }

      return filenames;
    }

    public void ParseDTAText(string dtapath)
    {
      FileStream fs = new FileStream(dtapath, FileMode.Open);
      BinaryReader r = new BinaryReader(fs, Encoding.ASCII);

      // verify signature
      char[] dta_sig = r.ReadChars(DTA_SIGNATURE.Length);
      string dta_sig_string = new string(dta_sig);
      Debug.Assert(DTA_SIGNATURE == dta_sig_string);

      // parse dta header
      Int32 dta_version = r.ReadInt32();
      Debug.Assert(dta_version == 43);

      Int32 engine_version_strlen = r.ReadInt32();
      Debug.Assert(engine_version_strlen == 7);

      char[] engine_version = r.ReadChars(engine_version_strlen);
      string engine_version_string = new string(engine_version);
      Debug.Assert(engine_version_string == "3.3.4.2");

      // parse game setup struct base
      AGSGameSetupStruct gameSetup = ParseGameSetupStructBase(r);

      // parse extended game setup struct (dtaver > 32)
      // parse save game info
      char[] save_guid = r.ReadChars(40);
      Debug.Assert(r.BaseStream.Position == 0xF85);

      char[] save_extension = r.ReadChars(20);
      Debug.Assert(r.BaseStream.Position == 0xF99);

      char[] save_folder = r.ReadChars(50);
      Debug.Assert(r.BaseStream.Position == 0xFCB);

      // parse font info
      byte[] font_flags = r.ReadBytes(gameSetup.fonts_count);
      Debug.Assert(r.BaseStream.Position == 0xFCE);

      byte[] font_outlines = r.ReadBytes(gameSetup.fonts_count);
      Debug.Assert(r.BaseStream.Position == 0xFD1);

      // parse sprite flags
      // dtaver >= 24
      Int32 sprites_count_max = r.ReadInt32();
      Debug.Assert(r.BaseStream.Position == 0xFD5);

      byte[] sprite_flags = r.ReadBytes(sprites_count_max);
      Debug.Assert(r.BaseStream.Position == 0x8505);

      // parse inventory items info
      AGSInventoryItemInfo[] inventoryItems = ParseInventoryItems(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x869D);

      // parse cursors info
      AGSCursorInfo[] cursors = ParseCursors(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x878D);

      // parse characters interaction scripts
      ParseInteractionScripts(r, gameSetup.characters_count);
      Debug.Assert(r.BaseStream.Position == 0x8870);

      // parse inventory items interaction scripts
      ParseInteractionScripts(r, gameSetup.inventory_items_count - 1);
      Debug.Assert(r.BaseStream.Position == 0x88FA);

      // parse dictionary
      if (gameSetup.load_dictionary != 0)
      {
        ParseDictionary(r);
        Debug.Assert(r.BaseStream.Position == 0x8A49);
      }

      // parse global script
      ParseCScript(r);
      Debug.Assert(r.BaseStream.Position == 0x11515);

      // parse dialog script
      if (dta_version > 37) // 3.1.0
      {
        ParseCScript(r);
        Debug.Assert(r.BaseStream.Position == 0x11F4E);
      }

      // parse other scripts
      if (dta_version >= 31) // 2.7.0
      {
        Int32 modules_count = r.ReadInt32();
        for (int i = 0; i < modules_count; ++i)
        {
          ParseCScript(r);
        }
        Debug.Assert(r.BaseStream.Position == 0x126D6);
      }

      // parse views
      if (dta_version > 32) // 2.7.2
      {
        AGSView[] views = ParseViews(r, ref gameSetup);
        Debug.Assert(r.BaseStream.Position == 0x14250);
      }

      // parse characters
      AGSAlignedStream ar = new AGSAlignedStream();
      for (int i = 0; i < gameSetup.characters_count; ++i)
      {
        ParseAGSCharacter(r, ar);
        ar.Reset();
      }
      Debug.Assert(r.BaseStream.Position == 0x17310);

      // parse lipsync
      if (dta_version >= 21) // 2.54
      {
        r.BaseStream.Seek(20 * 50, SeekOrigin.Current);
        Debug.Assert(r.BaseStream.Position == 0x176F8);
      }

      // parse global messages
      ParseGlobalMessages(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x176F8);

      // parse dialogs
      ParseDialogs(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x176F8);

      // parse guis
      ParseGUIs(r);
      Debug.Assert(r.BaseStream.Position == 0x1A611);

      // parse plugins
      ParsePlugins(r);
      Debug.Assert(r.BaseStream.Position == 0x1A619);

      // parse custom properties
      ParseCustomProperties(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x1A7AA);

      // parse audio clips
      ParseAudio(r);
      Debug.Assert(r.BaseStream.Position == 0x1AD06);

      // parse room names
      ParseRoomNames(r, ref gameSetup);
      Debug.Assert(r.BaseStream.Position == 0x1AE08);

      r.Close();
    }

    private string[] ParseRoomNames(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      Int32 count = r.ReadInt32();
      string[] names = new string[count];
      for (int i = 0; i < count; ++i)
      {
        Int32 id = r.ReadInt32();
        names[i] = r.ReadNullTerminatedString(3000);
      }

      return names;
    }

    private void ParseAudio(BinaryReader r)
    {
      // parse audio types
      ParseAudioTypes(r);
      Debug.Assert(r.BaseStream.Position == 0x1A7FE);

      // parse audio clips info
      ParseAudioClips(r);
      Debug.Assert(r.BaseStream.Position == 0x1AD02);

      Int32 score_clip_id = r.ReadInt32();
    }

    private void ParseAudioClips(BinaryReader r)
    {
      AGSAlignedStream ar = new AGSAlignedStream();
      Int32 clips_count = r.ReadInt32();
      for (int i = 0; i < clips_count; ++i)
      {
        Int32 id = ar.ReadInt32(r);
        char[] scriptname = ar.ReadFixedString(r, 30);
        char[] filename = ar.ReadFixedString(r, 15);
        byte type_bundling = ar.ReadByte(r);
        byte type = ar.ReadByte(r);
        byte type_file = ar.ReadByte(r);
        byte repeat_default = ar.ReadByte(r);
        Int16 priority_default = ar.ReadInt16(r);
        Int16 volume_default = ar.ReadInt16(r);
        Int32 reserved1 = ar.ReadInt32(r);

        ar.Reset();
      }
    }

    private void ParseAudioTypes(BinaryReader r)
    {
      Int32 types_count = r.ReadInt32();
      for (int i = 0; i < types_count; ++i)
      {
        Int32 id = r.ReadInt32();
        Int32 channels = r.ReadInt32();
        Int32 volume_dumping = r.ReadInt32();
        Int32 crossfade_speed = r.ReadInt32();
        Int32 reserved1 = r.ReadInt32();
      }
    }

    private void ParseCustomProperties(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      ParseSchema(r);
      
      // parse characters properties
      for (int i = 0; i < setup.characters_count; ++i)
      {
        ParsePropertyValues(r);
      }

      // parse inventory items properties
      for (int i = 0; i < setup.inventory_items_count; ++i)
      {
        ParsePropertyValues(r);
      }
      Debug.Assert(r.BaseStream.Position == 0x1A6D1);

      //NOTE(adm244): why are these in the "custom properties" section?
      // parse views script names
      string[] view_names = new string[setup.views_count];
      for (int i = 0; i < setup.views_count; ++i)
      {
        view_names[i] = r.ReadNullTerminatedString();
      }
      Debug.Assert(r.BaseStream.Position == 0x1A77E);

      // parse inventory items script names
      string[] item_names = new string[setup.inventory_items_count];
      for (int i = 0; i < setup.inventory_items_count; ++i)
      {
        item_names[i] = r.ReadNullTerminatedString();
      }
      Debug.Assert(r.BaseStream.Position == 0x1A7AA);

      // parse dialogs script names
      string[] dialog_names = new string[setup.dialogs_count];
      for (int i = 0; i < setup.dialogs_count; ++i)
      {
        dialog_names[i] = r.ReadNullTerminatedString();
      }
    }

    private void ParsePropertyValues(BinaryReader r)
    {
      Int32 version = r.ReadInt32();
      Debug.Assert(version == 1);

      Int32 count = r.ReadInt32();
      //TODO(adm244): test that on a real dta file
      for (int i = 0; i < count; ++i)
      {
        string name = r.ReadNullTerminatedString(200);
        string value = r.ReadNullTerminatedString(500);
      }
    }

    private void ParseSchema(BinaryReader r)
    {
      Int32 version = r.ReadInt32();
      Debug.Assert(version == 1);

      Int32 count = r.ReadInt32();
      for (int i = 0; i < count; ++i)
      {
        //TODO(adm244): parse schema
      }
    }

    private void ParsePlugins(BinaryReader r)
    {
      Int32 format = r.ReadInt32();
      Debug.Assert(format == 1);

      Int32 count = r.ReadInt32();
      for (int i = 0; i < count; ++i)
      {
        //TODO(adm244): parse plugins
      }
    }

    private void ParseGUIs(BinaryReader r)
    {
      // verify signature
      Int32 signature = r.ReadInt32();
      Debug.Assert((UInt32)signature == 0xCAFEBEEF);

      // parse header
      Int32 version = r.ReadInt32();
      Int32 count = r.ReadInt32();
      Debug.Assert((count >= 0) && (count <= 1000));

      // parse guis
      for (int i = 0; i < count; ++i)
      {
        //NOTE(adm244): I'm starting to suspect that the source for 3.3.4 Engine.App is
        // actually older than 3.3.4, because it doesn't contain some of these unknown int32's
        Int32 unknown1 = r.ReadInt32();

        char[] name = r.ReadChars(16);
        char[] onclick_handler = r.ReadChars(20);
        Int32 x = r.ReadInt32();
        Int32 y = r.ReadInt32();
        Int32 width = r.ReadInt32();
        Int32 height = r.ReadInt32();
        Int32 control_focus = r.ReadInt32();
        Int32 control_count = r.ReadInt32();
        Int32 popup_style = r.ReadInt32();
        Int32 popup_at_mouse_y = r.ReadInt32();
        Int32 background_color = r.ReadInt32();
        Int32 background_image = r.ReadInt32();
        Int32 foreground_color = r.ReadInt32();

        // savegame info
        Int32 mouse_over_control = r.ReadInt32();
        Int32 mouse_was_at_y = r.ReadInt32();
        Int32 mouse_was_at_x = r.ReadInt32();
        Int32 mouse_down_control = r.ReadInt32();
        Int32 highlight_control = r.ReadInt32();

        Int32 flags = r.ReadInt32();
        Int32 transparency = r.ReadInt32();
        Int32 z_order = r.ReadInt32();
        Int32 id = r.ReadInt32();
        Int32 padding = r.ReadInt32();

        // skip reserved variables
        r.BaseStream.Seek(5 * sizeof(Int32), SeekOrigin.Current);

        Int32 visibility_state = r.ReadInt32();

        // skip "unused" variables
        r.BaseStream.Seek(30 * sizeof(Int32), SeekOrigin.Current);

        Int32[] control_references = r.ReadArrayInt32(30);
      }

      // parse controls
      ParseGUIButtons(r);
      Debug.Assert(r.BaseStream.Position == 0x1A5BC);

      ParseGUILabels(r);
      Debug.Assert(r.BaseStream.Position == 0x1A5C0);

      ParseGUIInventoryWindows(r);
      Debug.Assert(r.BaseStream.Position == 0x1A605);

      ParseGUISliders(r);
      Debug.Assert(r.BaseStream.Position == 0x1A609);

      ParseGUITextBoxes(r);
      Debug.Assert(r.BaseStream.Position == 0x1A60D);

      ParseGUIListBoxes(r);
      Debug.Assert(r.BaseStream.Position == 0x1A611);
    }

    private void ParseGUIListBoxes(BinaryReader r)
    {
      Int32 count = r.ReadInt32();
      //TODO(adm244): test that on a real dta file
      for (int i = 0; i < count; ++i)
      {
        ParseGUIObject(r);

        // parse listbox info
        Int32 item_count = r.ReadInt32();
        
        // parse savegame info
        Int32 item_selected = r.ReadInt32();
        Int32 item_top = r.ReadInt32();
        Int32 mouse_x = r.ReadInt32();
        Int32 mouse_y = r.ReadInt32();
        Int32 row_height = r.ReadInt32();
        Int32 visible_items_count = r.ReadInt32();

        Int32 font = r.ReadInt32();
        Int32 text_color = r.ReadInt32();
        Int32 text_color_selected = r.ReadInt32();
        Int32 flags = r.ReadInt32();
        Int32 text_aligment = r.ReadInt32();
        Int32 reserved1 = r.ReadInt32();
        Int32 background_color_selected = r.ReadInt32();

        string[] items = new string[item_count];
        for (int item_index = 0; item_index < item_count; ++item_index)
        {
          items[item_index] = r.ReadNullTerminatedString();
        }

        if ((flags & 4) != 0)
        {
          // skip savegame info
          r.BaseStream.Seek(item_count * sizeof(Int16), SeekOrigin.Current);
        }
      }
    }

    private void ParseGUITextBoxes(BinaryReader r)
    {
      Int32 count = r.ReadInt32();
      //TODO(adm244): test that on a real dta file
      for (int i = 0; i < count; ++i)
      {
        ParseGUIObject(r);

        // parse textbox info
        char[] text = r.ReadChars(200);
        Int32 font = r.ReadInt32();
        Int32 text_color = r.ReadInt32();
        Int32 flags = r.ReadInt32();
      }
    }

    private void ParseGUISliders(BinaryReader r)
    {
      Int32 count = r.ReadInt32();
      //TODO(adm244): test that on a real dta file
      for (int i = 0; i < count; ++i)
      {
        ParseGUIObject(r);

        // parse slider info
        Int32 value_min = r.ReadInt32();
        Int32 value_max = r.ReadInt32();
        Int32 value = r.ReadInt32();

        // parse savegame info
        Int32 is_mouse_pressed = r.ReadInt32();

        Int32 handle_image = r.ReadInt32();
        Int32 handle_offset = r.ReadInt32();
        Int32 background_image = r.ReadInt32();
      }
    }

    private void ParseGUIInventoryWindows(BinaryReader r)
    {
      Int32 count = r.ReadInt32();
      for (int i = 0; i < count; ++i)
      {
        ParseGUIObject(r);

        // parse inventory window info
        Int32 character_id = r.ReadInt32();
        Int32 item_width = r.ReadInt32();
        Int32 item_height = r.ReadInt32();

        // parse savegame info
        Int32 item_top = r.ReadInt32();
      }
    }

    private void ParseGUILabels(BinaryReader r)
    {
      Int32 labels_count = r.ReadInt32();
      //TODO(adm244): test that on a real dta file
      for (int i = 0; i < labels_count; ++i)
      {
        ParseGUIObject(r);

        // parse label info
        string text = r.ReadString();
        Int32 font = r.ReadInt32();
        Int32 text_color = r.ReadInt32();
        Int32 text_aligment = r.ReadInt32();
      }
    }

    private void ParseGUIButtons(BinaryReader r)
    {
      Int32 count = r.ReadInt32();
      for (int i = 0; i < count; ++i)
      {
        ParseGUIObject(r);

        // parse button info
        Int32 image = r.ReadInt32();
        Int32 image_mouseover = r.ReadInt32();
        Int32 image_pushed = r.ReadInt32();
        Int32 image_current = r.ReadInt32();
        Int32 is_pushed = r.ReadInt32();
        Int32 is_mouseover = r.ReadInt32();
        Int32 font = r.ReadInt32();
        Int32 text_color = r.ReadInt32();
        Int32 left_click_action = r.ReadInt32();
        Int32 right_click_action = r.ReadInt32();
        Int32 left_click_data = r.ReadInt32();
        Int32 right_click_data = r.ReadInt32();
        char[] text = r.ReadChars(50);
        Int32 text_aligment = r.ReadInt32();
        Int32 reserved1 = r.ReadInt32();
      }
    }

    private void ParseGUIObject(BinaryReader r)
    {
      // parse gui object info
      Int32 flags = r.ReadInt32();
      Int32 x = r.ReadInt32();
      Int32 y = r.ReadInt32();
      Int32 width = r.ReadInt32();
      Int32 height = r.ReadInt32();
      Int32 z_order = r.ReadInt32();
      Int32 is_activated = r.ReadInt32();
      string name = r.ReadNullTerminatedString();

      Int32 events_count = r.ReadInt32();
      string[] event_functions = new string[events_count];
      for (int event_index = 0; event_index < events_count; ++event_index)
      {
        event_functions[event_index] = r.ReadNullTerminatedString();
      }
    }

    private void ParseDialogs(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      for (int i = 0; i < setup.dialogs_count; ++i)
      {
        //TODO(adm244): see DialogTopic::ReadFromFile
      }
    }

    private void ParseGlobalMessages(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      for (int i = 0; i < 500; ++i)
      {
        if (setup.global_messages[i] == 0) continue;
        // read encrypted string
        string global_message = AGSUtils.ReadEncryptedString(r);
      }
    }

    private void ParseAGSCharacter(BinaryReader r, AGSAlignedStream ar)
    {
      Int32 view_default = ar.ReadInt32(r);
      Int32 view_talk = ar.ReadInt32(r);
      Int32 view_normal = ar.ReadInt32(r);
      Int32 room = ar.ReadInt32(r);
      Int32 room_previous = ar.ReadInt32(r);
      Int32 x = ar.ReadInt32(r);
      Int32 y = ar.ReadInt32(r);
      Int32 wait = ar.ReadInt32(r);
      Int32 flags = ar.ReadInt32(r);
      Int16 following = ar.ReadInt16(r);
      Int16 followinfo = ar.ReadInt16(r);
      Int32 view_idle = ar.ReadInt16(r);
      Int16 idle_time = ar.ReadInt16(r);
      Int16 idle_left = ar.ReadInt16(r);
      Int16 transparency = ar.ReadInt16(r);
      Int16 baseline = ar.ReadInt16(r);
      Int32 active_invitem = ar.ReadInt32(r);
      Int32 talk_color = ar.ReadInt32(r);
      Int32 view_think = ar.ReadInt32(r);
      Int16 view_blink = ar.ReadInt16(r);
      Int16 blink_interval = ar.ReadInt16(r);
      Int16 blink_timer = ar.ReadInt16(r);
      Int16 blink_frame = ar.ReadInt16(r);
      Int16 walkspeed_y = ar.ReadInt16(r);
      Int16 picture_offset_y = ar.ReadInt16(r);
      Int32 z = ar.ReadInt32(r);
      Int32 wait_walk = ar.ReadInt32(r);
      Int16 speech_animation_speed = ar.ReadInt16(r);
      Int16 reserved1 = ar.ReadInt16(r);
      Int16 blocking_width = ar.ReadInt16(r);
      Int16 blocking_height = ar.ReadInt16(r);
      Int32 index_id = ar.ReadInt32(r);
      Int16 picture_offset_x = ar.ReadInt16(r);
      Int16 walk_wait_counter = ar.ReadInt16(r);
      Int16 loop = ar.ReadInt16(r);
      Int16 frame = ar.ReadInt16(r);
      Int16 walking = ar.ReadInt16(r);
      Int16 animating = ar.ReadInt16(r);
      Int16[] inventory = ar.ReadArrayInt16(r, 301);
      Int16 act_x = ar.ReadInt16(r);
      Int16 act_y = ar.ReadInt16(r);

      //NOTE(adm244): in source it doesn't exist, but in the actual dta file it's there
      Int16 unknown1 = ar.ReadInt16(r);
      Int16 unknown2 = ar.ReadInt16(r);

      char[] name = ar.ReadFixedString(r, 40);
      char[] name_script = ar.ReadFixedString(r, 20);

      //NOTE(adm244): in source it's a byte, but in the actual dta it's int16
      Int16 on = ar.ReadInt16(r);
    }

    private AGSView[] ParseViews(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      AGSView[] views = new AGSView[setup.views_count];
      for (int i = 0; i < setup.views_count; ++i)
      {
        Int16 loop_count = r.ReadInt16();

        views[i] = new AGSView();
        views[i].Loops = new AGSViewLoop[loop_count];
        for (int loop = 0; loop < loop_count; ++loop)
        {
          Int16 frames_count = r.ReadInt16();
          Int32 flags = r.ReadInt32();

          views[i].Loops[loop] = new AGSViewLoop();
          views[i].Loops[loop].Frames = new AGSViewLoopFrame[frames_count];
          views[i].Loops[loop].Flags = flags;

          AGSAlignedStream ar = new AGSAlignedStream();
          for (int frame = 0; frame < frames_count; ++frame)
          {
            views[i].Loops[loop].Frames[frame] = new AGSViewLoopFrame();

            views[i].Loops[loop].Frames[frame].picture = ar.ReadInt32(r);
            views[i].Loops[loop].Frames[frame].offset_x = ar.ReadInt16(r);
            views[i].Loops[loop].Frames[frame].offset_y = ar.ReadInt16(r);
            views[i].Loops[loop].Frames[frame].speed = ar.ReadInt16(r);
            views[i].Loops[loop].Frames[frame].flags = ar.ReadInt32(r);
            views[i].Loops[loop].Frames[frame].sound = ar.ReadInt32(r);
            views[i].Loops[loop].Frames[frame].reserved1 = ar.ReadInt32(r);
            views[i].Loops[loop].Frames[frame].reserved2 = ar.ReadInt32(r);

            ar.Reset();
          }
        }
      }

      return views;
    }

    private void ParseCScript(BinaryReader r)
    {
      // verify signature
      char[] scom_sig = r.ReadChars(4);
      string scom_sig_string = new string(scom_sig);
      Debug.Assert(scom_sig_string == SCRIPT_SIGNATURE);

      Int32 version = r.ReadInt32();
      //Debug.Assert(r.BaseStream.Position == 0x8A51);

      // read section sizes
      Int32 globaldata_size = r.ReadInt32();
      Int32 code_size = r.ReadInt32();
      Int32 strings_size = r.ReadInt32();
      //Debug.Assert(r.BaseStream.Position == 0x8A5D);

      // parse global data section
      if (globaldata_size > 0)
      {
        //NOTE(adm244): skip for now
        r.BaseStream.Seek(globaldata_size, SeekOrigin.Current);
      }

      // parse code section
      if (code_size > 0)
      {
        //NOTE(adm244): skip for now
        r.BaseStream.Seek(code_size * sizeof(Int32), SeekOrigin.Current);
      }
      //Debug.Assert(r.BaseStream.Position == 0xFD79);

      // parse strings section
      if (strings_size > 0)
      {
        //NOTE(adm244): sequence of null terminated strings
        byte[] buffer = r.ReadBytes(strings_size);
        //Debug.Assert(r.BaseStream.Position == 0xFFF2);

        string[] strs = AGSUtils.ConvertNullTerminatedSequence(buffer);
      }

      // parse fixups section
      Int32 fixups_count = r.ReadInt32();
      //NOTE(adm244): skip for now
      r.BaseStream.Seek(fixups_count, SeekOrigin.Current);
      //Debug.Assert(r.BaseStream.Position == 0x1017F);

      //NOTE(adm244): skip for now
      r.BaseStream.Seek(fixups_count * sizeof(Int32), SeekOrigin.Current);
      //Debug.Assert(r.BaseStream.Position == 0x107A3);

      // parse imports section
      Int32 imports_count = r.ReadInt32();
      string[] import_symbols = new string[imports_count];
      for (int i = 0; i < imports_count; ++i)
      {
        import_symbols[i] = r.ReadNullTerminatedString(300);
      }
      //Debug.Assert(r.BaseStream.Position == 0x11115);

      // parse exports section
      Int32 exports_count = r.ReadInt32();
      string[] export_symbols = new string[exports_count];
      for (int i = 0; i < exports_count; ++i)
      {
        export_symbols[i] = r.ReadNullTerminatedString(300);
        Int32 ptr = r.ReadInt32();
      }
      //Debug.Assert(r.BaseStream.Position == 0x114F8);

      // parse script sections
      Int32 sections_count = r.ReadInt32();
      for (int i = 0; i < sections_count; ++i)
      {
        string section_name = r.ReadNullTerminatedString(300);
        Int32 section_offset = r.ReadInt32();
      }
      //Debug.Assert(r.BaseStream.Position == 0x11511);

      // verify tail signature
      Int32 tail_sig = r.ReadInt32();
      Debug.Assert((UInt32)tail_sig == 0xBEEFCAFE);
    }

    private void ParseDictionary(BinaryReader r)
    {
      Int32 words_count = r.ReadInt32();
      string[] words = new string[words_count];
      for (int i = 0; i < words_count; ++i)
      {
        words[i] = AGSUtils.ReadEncryptedString(r);
        Int16 word_group = r.ReadInt16();
      }
    }

    private void ParseInteractionScripts(BinaryReader r, int count)
    {
      for (int i = 0; i < count; ++i)
      {
        Int32 events_count = r.ReadInt32();
        string[] event_names = new string[events_count];
        for (int j = 0; j < events_count; ++j)
        {
          event_names[j] = r.ReadNullTerminatedString(200);
        }
      }
    }

    private AGSCursorInfo[] ParseCursors(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      AGSAlignedStream ar = new AGSAlignedStream();
      AGSCursorInfo[] cursors = new AGSCursorInfo[setup.cursors_count];
      for (int i = 0; i < setup.cursors_count; ++i)
      {
        Int32 picture = ar.ReadInt32(r);
        Int16 hotspot_x = ar.ReadInt16(r);
        Int16 hotspot_y = ar.ReadInt16(r);
        Int16 view = ar.ReadInt16(r);
        char[] name = ar.ReadFixedString(r, 10);
        //NOTE(adm244): in engine source it's int8, but in the actual dta file it's an int32
        // might just be a padding issue here, double check that
        Int32 flags = ar.ReadInt32(r);

        ar.Reset();
      }

      return cursors;
    }

    private AGSInventoryItemInfo[] ParseInventoryItems(BinaryReader r, ref AGSGameSetupStruct setup)
    {
      AGSAlignedStream ar = new AGSAlignedStream();
      AGSInventoryItemInfo[] inventoryItems = new AGSInventoryItemInfo[setup.inventory_items_count];
      for (int i = 0; i < setup.inventory_items_count; ++i)
      {
        inventoryItems[i].name = ar.ReadFixedString(r, 25);
        //Debug.Assert(r.BaseStream.Position == 0x851E);

        inventoryItems[i].picture = ar.ReadInt32(r);
        //Debug.Assert(r.BaseStream.Position == 0x8525);

        inventoryItems[i].cursor_picture = ar.ReadInt32(r);
        //Debug.Assert(r.BaseStream.Position == 0x8529);

        inventoryItems[i].hotspot_x = ar.ReadInt32(r);
        //Debug.Assert(r.BaseStream.Position == 0x852D);

        inventoryItems[i].hotspot_y = ar.ReadInt32(r);
        //Debug.Assert(r.BaseStream.Position == 0x8531);

        inventoryItems[i].reserved = ar.ReadArrayInt32(r, 5);
        //Debug.Assert(r.BaseStream.Position == 0x8545);

        //NOTE(adm244): in engine source it's int8, but in the actual dta file it's an int32
        // might just be a padding issue here, double check that
        inventoryItems[i].flag = ar.ReadInt32(r);
        //Debug.Assert(r.BaseStream.Position == 0x8549);
      }
      return inventoryItems;
    }

    private AGSGameSetupStruct ParseGameSetupStructBase(BinaryReader r)
    {
      AGSGameSetupStruct setup = new AGSGameSetupStruct();

      AGSAlignedStream ar = new AGSAlignedStream();
      setup.name = ar.ReadFixedString(r, 50);
      Debug.Assert(r.BaseStream.Position == 0x5F);

      setup.options = ar.ReadArrayInt32(r, 100);
      Debug.Assert(r.BaseStream.Position == 0x1F1);

      setup.paluses = ar.ReadBytes(r, 256);
      Debug.Assert(r.BaseStream.Position == 0x2F1);

      //NOTE(adm244): reg: ABGR; mem: RGBA
      setup.defaultPallete = ar.ReadArrayInt32(r, 256);
      Debug.Assert(r.BaseStream.Position == 0x6F1);

      setup.views_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x6F5);

      setup.characters_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x6F9);

      setup.player_character_id = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x6FD);

      setup.total_score = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x701);

      setup.inventory_items_count = ar.ReadInt16(r);
      Debug.Assert(r.BaseStream.Position == 0x703);

      setup.dialogs_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x709);

      setup.dialog_messages_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x70D);

      setup.fonts_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x711);

      setup.color_depth = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x715);

      setup.target_win = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x719);

      setup.dialog_bullet = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x71D);

      setup.hotdot = ar.ReadInt16(r);
      Debug.Assert(r.BaseStream.Position == 0x71F);

      setup.hotdot_outter = ar.ReadInt16(r);
      Debug.Assert(r.BaseStream.Position == 0x721);

      setup.unique_id = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x725);

      setup.guis_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x729);

      setup.cursors_count = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x72D);

      setup.default_resolution = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x731);

      setup.default_lipsync_frame = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x735);

      setup.inventory_hotdot_sprite = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0x739);

      setup.reserved = ar.ReadArrayInt32(r, 17);
      Debug.Assert(r.BaseStream.Position == 0x77D);

      setup.global_messages = ar.ReadArrayInt32(r, 500);
      Debug.Assert(r.BaseStream.Position == 0xF4D);

      setup.load_dictionary = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0xF51);

      setup.some_globalscript_value = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0xF55);

      setup.some_chars_value = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0xF59);

      setup.is_scriptcompiled = ar.ReadInt32(r);
      Debug.Assert(r.BaseStream.Position == 0xF5D);

      return setup;
    }
  }
}