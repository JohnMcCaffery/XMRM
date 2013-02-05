/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.WorldX;
using System;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;
using System.Threading;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Sandboxed;

namespace StAndrews.ExternalMRM {
    public class SandboxedWorld : KillableProxy, IWorld {
        
        private IWorld _world;
        private SandboxedObjectAccessor _accessor;

        internal SandboxedWorld(IWorld world) {
            _world = world;
            _accessor = new SandboxedObjectAccessor(_world.Objects);
            _chatListener = new OnChatDelegate(TriggerChat);
            _newUserListener = new OnNewUserDelegate(TriggerNewUser);
        }

        private OnNewUserDelegate _newUserListener;
        private event OnNewUserDelegate _OnNewUser;
        public event OnNewUserDelegate OnNewUser {
            add {
                if (_OnNewUser == null)
                    _world.OnNewUser += _newUserListener;
                _OnNewUser += value;
            }
            remove {
                _OnNewUser -= value;
                if (_OnNewUser == null)
                    _world.OnNewUser -= _newUserListener;
            }
        }
        private void TriggerNewUser(IWorld world, NewUserEventArgs args) {
            _world = world;
            Root.QueueEvent(() => _OnNewUser(this, args));
        }
        
        private OnChatDelegate _chatListener;
        private event OnChatDelegate _OnChat;
        public event OnChatDelegate OnChat {
            add {
                if (_OnChat == null)
                    _world.OnChat += _chatListener;
                _OnChat += value;
            }
            remove {
                _OnChat -= value;
                if (_OnChat == null)
                    _world.OnChat -= _chatListener;
            }
        }
        private void TriggerChat(IWorld world, ChatEventArgs args) {
            _world = world;
            Root.QueueEvent(() => _OnChat(this, args));
        }

        public IObjectAccessor Objects {
            get { return _accessor; }
        }

        public IAvatar[] Avatars {
            get { return _world.Avatars; }
        }

        public IParcel[] Parcels {
            get { return _world.Parcels; }
        }

        public IHeightmap Terrain {
            get { return _world.Terrain; }
        }

        public IWorldAudio Audio {
            get { return _world.Audio; }
        }

        internal void Shutdown() {
            _accessor.FullClear();
            _accessor.Kill(TimeSpan.FromMinutes(1));
        }
    }
}
