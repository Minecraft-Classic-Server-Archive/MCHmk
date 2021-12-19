/*
    Copyright 2016 Jjp137

    This file has been changed from the original source code by MCForge.

    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at

    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html

    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
*/

/*
Copyright (C) 2010-2013 David Mitchell

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MCHmk.Commands {
    public sealed class CmdReplace : Command {
        /// <summary>
        /// Name of the key used to store and retrieve the block type that the other blocks will be replaced with.
        /// </summary>
        private readonly string _blockKey = "replace_block";
        /// <summary>
        /// Name of the key used to store and retrieve the list of block types that the command will replace.
        /// </summary>
        private readonly string _oldTypesKey = "replace_old_types";

        private ReadOnlyCollection<string> _keywords = Array.AsReadOnly<string>(
            new string[] {"block", "level", "lvl", "map"});

        public override string Name {
            get {
                return "replace";
            }
        }
        public override string Shortcut {
            get {
                return "re";
            }
        }
        public override string Type {
            get {
                return "build";
            }
        }
        public override ReadOnlyCollection<string> Keywords {
            get {
                return _keywords;
            }
        }
        public override bool MuseumUsable {
            get {
                return false;
            }
        }
        public override int DefaultRank {
            get {
                return DefaultRankValue.AdvBuilder;
            }
        }
        public CmdReplace(Server s) : base(s) { }

        public override void Use(Player p, string args) {
            string[] splitArgs = args.Split(' ');
            if (splitArgs.Length != 2) {
                p.SendMessage("Invalid number of arguments!");
                Help(p);
                return;
            }

            List<string> oldTypes;

            oldTypes = new List<string>(splitArgs[0].Split(','));

            oldTypes = oldTypes.Distinct().ToList(); // Remove duplicates

            List<string> invalid = new List<string>(); //Check for invalid blocks
            foreach (string name in oldTypes) {
                if (BlockData.Ushort(name) == BlockId.Null) {
                    invalid.Add(name);
                }
            }
            if (BlockData.Ushort(splitArgs[1]) == BlockId.Null) {
                invalid.Add(splitArgs[1]);
            }
            if (invalid.Count > 0) {
                p.SendMessage(String.Format("Invalid block{0}: {1}", invalid.Count == 1 ? String.Empty : "s", String.Join(", ",
                                            invalid.ToArray())));
                return;
            }

            if (oldTypes.Contains(splitArgs[1])) {
                oldTypes.Remove(splitArgs[1]);
            }
            if (oldTypes.Count < 1) {
                p.SendMessage("Replacing a block with the same one would be pointless!");
                return;
            }

            List<BlockId> realOldTypes = new List<BlockId>();
            foreach (string name in oldTypes) {
                realOldTypes.Add(BlockData.Ushort(name));
            }
            BlockId newType = BlockData.Ushort(splitArgs[1]);

            foreach (BlockId type in realOldTypes) {
                if (!_s.blockPerms.CanPlace(p, type) && !BlockData.BuildIn(type)) {
                    p.SendMessage("Cannot replace that.");
                    return;
                }
            }

            if (!_s.blockPerms.CanPlace(p, newType)) {
                p.SendMessage("Cannot place that.");
                return;
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data[_oldTypesKey] = realOldTypes;
            data[_blockKey] = newType;

            const string prompt = "Place two blocks to determine the edges.";
            TwoBlockSelection.Start(p, data, prompt, SelectionFinished);
        }

        /// <summary>
        /// Called when a player has finished selecting the area where the replacement should be performed.
        /// </summary>
        /// <param name="p"> The player that has selected both blocks. <seealso cref="Player"/></param>
        /// <param name="c"> Data associated with the second block's selection. <seealso cref="CommandTempData"/></param>
        private void SelectionFinished(Player p, CommandTempData c) {
            // Obtain the coordinates of both corners. The first corner is stored in the CommandTempData's Dictionary,
            // while the second corner is contained within the X, Y, and Z properties of the CommandTempData since
            // that block change occurred just now.
            ushort x1 = c.GetData<ushort>(TwoBlockSelection.XKey);
            ushort y1 = c.GetData<ushort>(TwoBlockSelection.YKey);
            ushort z1 = c.GetData<ushort>(TwoBlockSelection.ZKey);

            ushort x2 = c.X;
            ushort y2 = c.Y;
            ushort z2 = c.Z;

            List<BlockId> oldTypes = c.GetData<List<BlockId>>(_oldTypesKey);
            BlockId newType = c.GetData<BlockId>(_blockKey);

            List<UShortCoords> buffer = new List<UShortCoords>();

            for (ushort xx = Math.Min(x1, x2); xx <= Math.Max(x1, x2); ++xx) {
                for (ushort yy = Math.Min(y1, y2); yy <= Math.Max(y1, y2); ++yy) {
                    for (ushort zz = Math.Min(z1, z2); zz <= Math.Max(z1, z2); ++zz) {
                        if (oldTypes.Contains(p.level.GetTile(xx, yy, zz))) {
                            buffer.Add(new UShortCoords(xx, yy, zz));
                        }
                    }
                }
            }

            if (buffer.Count > p.rank.maxBlocks) {
                p.SendMessage("You tried to replace " + buffer.Count + " blocks.");
                p.SendMessage("You cannot replace more than " + p.rank.maxBlocks + ".");

                TwoBlockSelection.RestartIfStatic(p, c, SelectionFinished, _oldTypesKey, _blockKey);
                return;
            }

            p.SendMessage(buffer.Count.ToString() + " blocks.");

            if (p.level.bufferblocks && !p.level.Instant) {
                buffer.ForEach(delegate(UShortCoords pos) {
                    _s.blockQueue.Addblock(p, pos.X, pos.Y, pos.Z, newType);
                });
            }
            else {
                buffer.ForEach(delegate(UShortCoords pos) {
                    p.level.Blockchange(p, pos.X, pos.Y, pos.Z, newType);
                });
            }

            TwoBlockSelection.RestartIfStatic(p, c, SelectionFinished, _oldTypesKey, _blockKey);
        }
        
        /// <summary>
        /// Called when /help is used on /replace.
        /// </summary>
        /// <param name="p"> The player that used the /help command. </param>
        public override void Help(Player p) {
            p.SendMessage("/replace <old_block?> <new_block?> - Replaces all blocks of the " +
                               "given types with another type within a given cuboid.");
            p.SendMessage("'old_block' can be a comma-separated list of block types.");
        }
    }
}
