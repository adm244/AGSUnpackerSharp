﻿using System;
using System.Diagnostics;
using System.IO;

namespace AGSUnpackerSharp.Shared.Interaction
{
  public class AGSInteraction
  {
    public AGSInteractionCommandsList[] events;
    public Int32 version;
    public Int32[] event_responses;

    public AGSInteraction()
    {
      events = new AGSInteractionCommandsList[0];
      version = 1;
    }

    public void LoadFromStream(BinaryReader r)
    {
      Int32 version = r.ReadInt32();
      Debug.Assert(version == 1);

      Int32 events_count = r.ReadInt32();
      events = new AGSInteractionCommandsList[events_count];

      for (int i = 0; i < events.Length; ++i)
      {
        events[i] = new AGSInteractionCommandsList();
        events[i].type = r.ReadInt32();
      }

      event_responses = r.ReadArrayInt32(events.Length);
      for (int i = 0; i < event_responses.Length; ++i)
      {
        events[i] = null;

        if (event_responses[i] == 0)
          continue;

        events[i] = new AGSInteractionCommandsList();
        events[i].LoadFromStream(r);
      }
    }

    public void WriteToStream(BinaryWriter w)
    {
      w.Write((Int32)version);
      w.Write((Int32)events.Length);

      for (int i = 0; i < events.Length; ++i)
        w.Write((Int32)events[i].type);

      for (int i = 0; i < event_responses.Length; ++i)
        w.Write((Int32)event_responses[i]);

      for (int i = 0; i < events.Length; ++i)
      {
        if (events[i] == null)
          continue;

        events[i].WriteToStream(w);
      }
    }
  }
}
