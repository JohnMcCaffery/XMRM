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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenMetaverse;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Object;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;
using System.Runtime.Remoting.Lifetime;

namespace StAndrews.ExternalMRM {
    public class SandboxedObject : KillableProxyChild, IObject {

        private IObject _obj;
        
        internal SandboxedObject(IObject obj, ISponsor sponsor) : base (sponsor) {
            if (obj == null)
                throw new ArgumentException("Cannot instantiate an XObject with a null IObject.");
            _obj = obj;
            _touchListener = new OnTouchDelegate(TriggerTouch);
        }

        private OnTouchDelegate _touchListener;
        private event OnTouchDelegate _OnTouch;
        public event OnTouchDelegate OnTouch {
            add {
                if (_OnTouch == null && _obj.Exists)
                    _obj.OnTouch += _touchListener;
                _OnTouch += value;
            }
            remove {
                _OnTouch -= value;
                if (_OnTouch == null && _obj.Exists)
                    _obj.OnTouch -= _touchListener;
            }
        }
        private void TriggerTouch(IObject sender, TouchEventArgs args) {
            _obj = sender;
            OpenSim.Region.OptionalModules.Scripting.Minimodule.Sandboxed.Root.QueueEvent(() => _OnTouch(this, args));
        }

        public bool Exists {
            get { return _obj.Exists; }
        }

        public uint LocalID {
            get { return _obj.LocalID; }
        }

        public string Description {
            get {
                return _obj.Description;
            }
            set {
                _obj.Description = value;
            }
        }

        public UUID OwnerId {
            get { return _obj.OwnerId; }
        }

        public UUID CreatorId {
            get { return _obj.CreatorId; }
        }

        public IObject Root {
            get { return _obj.Root; }
        }

        public IObject[] Children {
            get { return _obj.Children.Select(child => new SandboxedObject(child, Sponsor)).ToArray(); }
        }

        public IObjectMaterial[] Materials {
            get { return _obj.Materials; }
        }

        public Vector3 Scale {
            get {
                return _obj.Scale;
            }
            set {
                _obj.Scale = value;
            }
        }

        public Quaternion WorldRotation {
            get {
                return _obj.WorldRotation;
            }
            set {
                _obj.WorldRotation = value;
            }
        }

        public Quaternion OffsetRotation {
            get {
                return _obj.OffsetRotation;
            }
            set {
                _obj.OffsetRotation = value;
            }
        }

        public Vector3 OffsetPosition {
            get {
                return _obj.OffsetPosition;
            }
            set {
                _obj.OffsetPosition = value;
            }
        }

        public Vector3 SitTarget {
            get {
                return _obj.SitTarget;
            }
            set {
                _obj.SitTarget = value;
            }
        }

        public string SitTargetText {
            get {
                return _obj.SitTargetText;
            }
            set {
                _obj.SitTargetText = value;
            }
        }

        public string TouchText {
            get {
                return _obj.TouchText;
            }
            set {
                _obj.TouchText = value;
            }
        }

        public string Text {
            get {
                return _obj.Text;
            }
            set {
                _obj.Text = value;
            }
        }

        public bool IsAttachment {
            get { return _obj.IsAttachment; }
        }

        public bool IsRotationLockedX {
            get {
                return _obj.IsRotationLockedX;
            }
            set {
                _obj.IsRotationLockedX = value;
            }
        }

        public bool IsRotationLockedY {
            get {
                return _obj.IsRotationLockedY;
            }
            set {
                _obj.IsRotationLockedY = value;
            }
        }

        public bool IsRotationLockedZ {
            get {
                return _obj.IsRotationLockedZ;
            }
            set {
                _obj.IsRotationLockedZ = value;
            }
        }

        public bool IsSandboxed {
            get {
                return _obj.IsSandboxed;
            }
            set {
                _obj.IsSandboxed = value;
            }
        }

        public bool IsImmotile {
            get {
                return _obj.IsImmotile;
            }
            set {
                _obj.IsImmotile = value;
            }
        }

        public bool IsAlwaysReturned {
            get {
                return _obj.IsAlwaysReturned;
            }
            set {
                _obj.IsAlwaysReturned = value;
            }
        }

        public bool IsTemporary {
            get {
                return _obj.IsTemporary;
            }
            set {
                _obj.IsTemporary = value;
            }
        }

        public bool IsFlexible {
            get {
                return _obj.IsFlexible;
            }
            set {
                _obj.IsFlexible = value;
            }
        }

        public IObjectShape Shape {
            get { return _obj.Shape; }
        }

        public PhysicsMaterial PhysicsMaterial {
            get {
                return _obj.PhysicsMaterial;
            }
            set {
                _obj.PhysicsMaterial = value;
            }
        }

        public IObjectPhysics Physics {
            get { return _obj.Physics; }
        }

        public IObjectSound Sound {
            get { return _obj.Sound; }
        }

        public void Say(string msg) {
            _obj.Say(msg);
        }

        public void Say(string msg, int channel) {
            _obj.Say(msg, channel);
        }

        public void Say(string msg, int channel, bool broadcast, MRMChatTypeEnum type) {
            _obj.Say(msg, channel, broadcast, type);
        }

        public void Say(string msg, int channel, ChatTargetEnum target) {
            _obj.Say(msg, channel, target);
        }

        public void Dialog(UUID avatar, string message, string[] buttons, int chat_channel) {
            _obj.Dialog(avatar, message, buttons, chat_channel);
        }

        public IObjectInventory Inventory {
            get { return _obj.Inventory; }
        }

        public string Name {
            get {
                return _obj.Name;
            }
            set {
                _obj.Name = value;
            }
        }

        public UUID GlobalID {
            get { return _obj.GlobalID; }
        }

        public Vector3 WorldPosition {
            get {
                return _obj.WorldPosition;
            }
            set {
                _obj.WorldPosition = value;
            }
        }
    }
}
