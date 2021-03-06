﻿/*
 * m4a2s is aprogram to to dump sound files from m4a/mp2k GBA games to pseudo-assembly source files
 * Copyright (C) 2015 ipatix
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301  USA
 */

using System.Collections;

namespace m4a2s
{
    static class Index
    {
        private static Hashtable _hashtable;
        private static int _numSongs;

        private static string _songGuids = "seq";
        private static string _bankGuids = "bank";
        private static string _mapGuids = "map";
        private static string _drumGuids = "drums";
        private static string _waveGuids = "wave";
        private static string _gbwaveGuids = "gbwave";

        public static Hashtable GetHashtable()
        {
            return _hashtable;
        }

        public static void IndexRom()
        {
            /*
             * create a new Hashtable instance where all our entities will end up
             */
            _hashtable = new Hashtable();
            _numSongs = GetNumSongs();
            Rom.NumSongs = _numSongs;
            /*
             * now we will index all our songs
             */

            Rom.Reader.BaseStream.Position = Rom.SongtableOffset;

            int numVoicegroups = 0;
            int numDrumtables = 0;
            int numSamples = 0;
            int numWaves = 0;
            int numMaps = 0;

            for (int currentSong = 0; currentSong < _numSongs; currentSong++)
            {
                // hash all songs
                Rom.Reader.BaseStream.Position = Rom.SongtableOffset + (currentSong * 8);
                int currentSongPointer = Rom.Reader.ReadInt32() - Rom.Map;
                Rom.Reader.BaseStream.Position = currentSongPointer;
                if (Rom.Reader.ReadByte() <= 0) continue;
                Rom.Reader.BaseStream.Position += 3;
                if (!_hashtable.Contains(currentSongPointer))
                    _hashtable.Add(currentSongPointer, new Entity(EntityType.Song, currentSongPointer, _songGuids + "_" + currentSong.ToString("D3"), -1));
                // now hash the voicegroup
                int voicegroupOffset = Rom.Reader.ReadInt32() - Rom.Map;
                if (!_hashtable.Contains(voicegroupOffset))
                {
                    _hashtable.Add(voicegroupOffset,
                        new Entity(EntityType.Bank, voicegroupOffset, _bankGuids + "_" + numVoicegroups++.ToString("D3"),
                            128));

                    for (int i = 0; i < 128; i++) // 128 = amount of instruments in voicegroup
                    {
                        Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i);
                            // 12 = length of instrument declaration
                        uint instrumentType = Rom.Reader.ReadUInt32();
                        instrumentType &= 0xFF; // cut off unimportant information
                        if (instrumentType == 0x0 || instrumentType == 0x8 || instrumentType == 0x10 ||
                            instrumentType == 0x18)
                        {
                            /*
                         * This seems to be a direct instrument declaration, let's collect the wave sample in our hashtable
                         */
                            Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                            int samplePointer = Rom.Reader.ReadInt32() - Rom.Map;
                            if (IsValidRectifiedPointer(samplePointer) && !_hashtable.Contains(samplePointer))
                                _hashtable.Add(samplePointer,
                                    new Entity(EntityType.Wave, samplePointer,
                                        _waveGuids + "_" + numSamples++.ToString("D3"), -1));
                        }
                        else if (instrumentType == 0x03 || instrumentType == 0xB)
                        {
                            Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                            int wavePointer = Rom.Reader.ReadInt32() - Rom.Map;
                            if (IsValidRectifiedPointer(wavePointer) && !_hashtable.Contains(wavePointer))
                                _hashtable.Add(wavePointer,
                                    new Entity(EntityType.GbWave, wavePointer,
                                        _gbwaveGuids + "_" + numWaves++.ToString("D3"), -1));
                        }
                        else if (instrumentType == 0x40)
                        {
                            Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                            int subInstruments = Rom.Reader.ReadInt32() - Rom.Map;
                            int keyMapTable = Rom.Reader.ReadInt32() - Rom.Map;

                            if (IsValidRectifiedPointer(keyMapTable))
                            {
                                if (!_hashtable.Contains(keyMapTable))
                                    _hashtable.Add(keyMapTable,
                                        new Entity(EntityType.KeyMap, keyMapTable,
                                            _mapGuids + "_" + numMaps++.ToString("D3"), -1));

                                int numSubInstruments = GetNumInstrumentsByKeyMap(keyMapTable);

                                if (IsValidRectifiedPointer(subInstruments) && !_hashtable.Contains(subInstruments))
                                {
                                    _hashtable.Add(subInstruments,
                                        new Entity(EntityType.Bank, subInstruments,
                                            _bankGuids + "_" + numVoicegroups++.ToString("D3"), numSubInstruments));
                                    for (int j = 0; j < numSubInstruments; j++)
                                    {
                                        Rom.Reader.BaseStream.Position = subInstruments + (12*j);
                                        uint subInstrumentType = Rom.Reader.ReadUInt32();
                                        subInstrumentType &= 0xFF;

                                        if (subInstrumentType == 0x0 || subInstrumentType == 0x8 ||
                                            subInstrumentType == 0x18 ||
                                            subInstrumentType == 0x10)
                                        {
                                            Rom.Reader.BaseStream.Position = subInstruments + (12*j) + 4;
                                            int samplePointer = Rom.Reader.ReadInt32() - Rom.Map;
                                            if (IsValidRectifiedPointer(samplePointer) && !_hashtable.Contains(samplePointer))
                                                _hashtable.Add(samplePointer,
                                                    new Entity(EntityType.Wave, samplePointer,
                                                        _waveGuids + "_" + numSamples++.ToString("D3"), 128));

                                        }
                                        else if (instrumentType == 0x3 || instrumentType == 0xB)
                                        {
                                            Rom.Reader.BaseStream.Position = subInstruments + (12*j) + 4;
                                            int wavePointer = Rom.Reader.ReadInt32() - Rom.Map;
                                            if (IsValidRectifiedPointer(wavePointer) && !_hashtable.Contains(wavePointer))
                                                _hashtable.Add(wavePointer,
                                                    new Entity(EntityType.Wave, wavePointer,
                                                        _gbwaveGuids + "_" + numWaves++.ToString("D3"), -1));
                                        }
                                    }
                                } // end of sub instrument verification
                            } // end of keymap verification
                        }
                        else if (instrumentType == 0x80)
                        {
                            Rom.Reader.BaseStream.Position = voicegroupOffset + (12*i) + 4;
                            int subVoicegroup = Rom.Reader.ReadInt32() - Rom.Map;
                            /*
                         * let's do the same procedute for this subtable
                         */
                            if (IsValidRectifiedPointer(subVoicegroup) && !_hashtable.Contains(subVoicegroup))
                            {
                                _hashtable.Add(subVoicegroup,
                                    new Entity(EntityType.Bank, subVoicegroup,
                                        _drumGuids + "_" + numDrumtables++.ToString("D3"), 128));
                                for (int j = 0; j < 128; j++)
                                {
                                    Rom.Reader.BaseStream.Position = subVoicegroup + (12*j);
                                    uint subInstrumentType = Rom.Reader.ReadUInt32();
                                    subInstrumentType &= 0xFF;

                                    if (subInstrumentType == 0x0 || subInstrumentType == 0x8 ||
                                        subInstrumentType == 0x18 ||
                                        subInstrumentType == 0x10)
                                    {
                                        Rom.Reader.BaseStream.Position = subVoicegroup + (12*j) + 4;
                                        int samplePointer = Rom.Reader.ReadInt32() - Rom.Map;
                                        if (IsValidRectifiedPointer(samplePointer) && !_hashtable.Contains(samplePointer))
                                            _hashtable.Add(samplePointer,
                                                new Entity(EntityType.Wave, samplePointer,
                                                    _waveGuids + "_" + numSamples++.ToString("D3"), 128));

                                    }
                                    else if (instrumentType == 0x3 || instrumentType == 0xB)
                                    {
                                        Rom.Reader.BaseStream.Position = subVoicegroup + (12*j) + 4;
                                        int wavePointer = Rom.Reader.ReadInt32() - Rom.Map;
                                        if (IsValidRectifiedPointer(wavePointer) && !_hashtable.Contains(wavePointer))
                                            _hashtable.Add(wavePointer,
                                                new Entity(EntityType.Wave, wavePointer,
                                                    _gbwaveGuids + "_" + numWaves++.ToString("D3"), -1));
                                    }
                                }
                            }
                        }
                    } // end of voicegroup loop
                }
            } // end of song loop

            /*
             * Now we should've put everything in the hashtable without duplicates
             */
        }

        private static bool IsValidSong(int songNum)
        {
            /*
             * First we need to make sure we seek to the position where the song is located
             * After that we save song header information into variables
             */
            Rom.Reader.BaseStream.Position = Rom.SongtableOffset + (songNum*8);
            int songPos = Rom.Reader.ReadInt32();
            if (!IsValidPointer(songPos)) return false;
            Rom.Reader.BaseStream.Position = songPos - Rom.Map;
            byte numTracks = Rom.Reader.ReadByte();
            byte numBlocks = Rom.Reader.ReadByte();
            byte priority = Rom.Reader.ReadByte();
            byte reverb = Rom.Reader.ReadByte();

            int voicegroupOffset = Rom.Reader.ReadInt32();
            /*
             * the 3 lower bytes aren't needed for the verification but we might use it in the *future*
             */

            if (numTracks > 0 && !IsValidPointer(voicegroupOffset)) return false;

            for (int i = 0; i < numTracks; i++)
            {
                if (!IsValidPointer(Rom.Reader.ReadInt32())) return false;
            }

            return true;
        }

        public static bool IsValidPointer(int pointer)
        {
            if (pointer - Rom.Map < 0 || pointer - Rom.Map >= Rom.RomSize) return false;
            return true;
        }

        public static bool IsValidRectifiedPointer(int pointer)
        {
            if (pointer < 0 || pointer >= Rom.RomSize) return false;
            return true;
        }

        private static int GetNumSongs()
        {
            int numSongs = 0;

            while (IsValidSong(numSongs)) numSongs++;
            return numSongs;
        }

        private static int GetNumInstrumentsByKeyMap(int keyMapPointer)
        {
            Rom.Reader.BaseStream.Position = keyMapPointer;
            byte biggestInstrument = 0;
            for (int i = 0; i < 128; i++)
            {
                byte mapping = Rom.Reader.ReadByte();
                if (mapping > biggestInstrument && mapping < 128) biggestInstrument = mapping;
            }
            return biggestInstrument + 1;
        }
    }
}
