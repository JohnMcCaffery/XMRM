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
using System.Security;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Object;
using OpenSim.Region.Physics.Manager;
using PrimType=OpenSim.Region.OptionalModules.Scripting.Minimodule.Object.PrimType;
using SculptType=OpenSim.Region.OptionalModules.Scripting.Minimodule.Object.SculptType;
using log4net;
using System.Runtime.Remoting.Lifetime;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class SOPObject : KillableProxy, IObject, IObjectPhysics, IObjectShape, IObjectSound, IDisposable {
        private readonly Scene m_rootScene;
        private readonly uint m_localID;
        private readonly ISecurityCredential m_security;
        private readonly ObjectAccessor m_accessor;
        private readonly UUID m_id;

        private readonly List<IObjectMaterial> m_materials = new List<IObjectMaterial>();

        private readonly EventManager.ObjectGrabDelegate m_touchListener;

        [Obsolete("Replace with 'credential' constructor [security]")]
        public SOPObject(Scene rootScene, uint localID, ObjectAccessor accessor)
        {
            m_rootScene = rootScene;
            m_localID = localID;
            m_accessor = accessor;
            m_id = GetSOP().UUID;
            _touchListeners = new List<OnTouchDelegate>();
            m_touchListener = new EventManager.ObjectGrabDelegate(EventManager_OnObjectGrab);
            _name = GetSOP().Name;
            CurrentCount++;
        }

        public SOPObject(Scene rootScene, uint localID, ISecurityCredential credential, ObjectAccessor accessor)
        {
            m_rootScene = rootScene;
            m_localID = localID;
            m_security = credential;
            m_accessor = accessor;
            m_id = GetSOP().UUID;
            _touchListeners = new List<OnTouchDelegate>();
            m_touchListener = new EventManager.ObjectGrabDelegate(EventManager_OnObjectGrab);
            _name = GetSOP().Name;
            CurrentCount++;
            TotalCount++;
        }

        private readonly string _name;
        public static int CurrentCount;
        public static int TotalCount;

        ~SOPObject() {
            Dispose();
            CurrentCount--;
            //Console.WriteLine(_name + " Destroyed.");
        }

        public void Dispose() {
            lock (_touchListeners)
                foreach (var listener in _touchListeners.ToArray())
                    OnTouch -= listener;
            Kill();
        }

        /// <summary>
        /// This needs to run very, very quickly.
        /// It is utilized in nearly every property and method.
        /// </summary>
        /// <returns></returns>
        private SceneObjectPart GetSOP()
        {
            return m_rootScene.GetSceneObjectPart(m_localID);
        }

        private bool CanEdit()
        {
            if (Exists && !m_accessor.IsGod && !m_security.CanEditObject(Root))
            {
                throw new SecurityException("Insufficient Permission to edit object with UUID [" + m_id + "]");
            }
            return true;
        }

        #region OnTouch

        private readonly List<OnTouchDelegate> _touchListeners;
        private event OnTouchDelegate _OnTouch;
        private bool _OnTouchActive = false;

        public event OnTouchDelegate OnTouch
        {
            add
            {
                if (CanEdit())
                {
                    if (!_OnTouchActive)
                    {
                        SceneObjectPart sop = GetSOP();
                        if (sop == null)
                            return;
                        lock (_listenerCounts) {
                            if (!_listenerCounts.ContainsKey(GlobalID)) {
                                sop.Flags |= PrimFlags.Touch;
                                _listenerCounts.Add(GlobalID, 0);
                            } else
                                _listenerCounts[GlobalID]++;
                        }
                        _OnTouchActive = true;
                        m_rootScene.EventManager.OnObjectGrab += m_touchListener;
                        sop.ScheduleFullUpdate();
                        m_rootScene.Update();
                    }

                    _touchListeners.Add(value);
                    _OnTouch += value;
                }
            }
            remove {
                _OnTouch -= value;
                lock(_touchListeners)
                    _touchListeners.Remove(value);

                if (_OnTouch == null) {
                    SceneObjectPart sop = GetSOP();
                    if (sop == null)
                        return;
                    lock (_listenerCounts) {
                        if (!_listenerCounts.ContainsKey(GlobalID) || _listenerCounts[GlobalID] == 1) {
                            sop.Flags &= ~PrimFlags.Touch;
                            _listenerCounts.Remove(GlobalID);
                        } else
                            _listenerCounts[GlobalID]--;
                    }
                    _OnTouchActive = false;
                    m_rootScene.EventManager.OnObjectGrab -= m_touchListener;
                    sop.ScheduleFullUpdate();
                    m_rootScene.Update();
                }
            }
        }

        private static readonly Dictionary<UUID, int> _listenerCounts = new Dictionary<UUID, int>();

        void EventManager_OnObjectGrab(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            if (_OnTouchActive && m_localID == localID)
            {
                TouchEventArgs e = new TouchEventArgs();
                e.Avatar = new SPAvatar(m_rootScene, remoteClient.AgentId, m_security, m_accessor);
                e.TouchBiNormal = surfaceArgs.Binormal;
                e.TouchMaterialIndex = surfaceArgs.FaceIndex;
                e.TouchNormal = surfaceArgs.Normal;
                e.TouchPosition = surfaceArgs.Position;
                e.TouchST = new Vector2(surfaceArgs.STCoord.X, surfaceArgs.STCoord.Y);
                e.TouchUV = new Vector2(surfaceArgs.UVCoord.X, surfaceArgs.UVCoord.Y);
                e.LinkNumber = originalID;

                IObject sender = this;

                if (_OnTouch != null)
                    _OnTouch(sender, e);
            }
        }

        #endregion

        public bool Exists
        {
            get { return GetSOP() != null; }
        }

        public uint LocalID
        {
            get { return m_localID; }
        }

        public UUID GlobalID
        {
            get { return m_id; }
        }

        public string Name
        {
            get {
                SceneObjectPart sop = GetSOP();
                if (sop == null)
                    return null;
                return sop.Name; }
            set
            {
                if (CanEdit())
                    GetSOP().Name = value;
            }
        }

        public string Description
        {
            get { return GetSOP().Description; }
            set
            {
                if (CanEdit()) 
                    GetSOP().Description = value;
            }
        }

        public UUID OwnerId
        {
            get { return GetSOP().OwnerID;}
        }

        public UUID CreatorId
        {
            get { return GetSOP().CreatorID;}
        }

        public IObject[] Children
        {
            get
            {
                SceneObjectPart my = GetSOP();
                IObject[] rets = null;

                int total = my.ParentGroup.PrimCount;

                rets = new IObject[total];

                int i = 0;
                    
                foreach (SceneObjectPart part in my.ParentGroup.Parts)
                {
                    rets[i++] = m_accessor[part.LocalId];
                }

                return rets;
            }
        }

        public IObject Root
        {
            get { return m_accessor[GetSOP().ParentGroup.RootPart.LocalId]; }
        }

        public IObjectMaterial[] Materials
        {
            get
            {
                SceneObjectPart sop = GetSOP();
                //int sides = getNumberOfSides(sop);
                //if (m_materials.Count > sides)
                //    return m_materials.GetRange(0, sides).ToArray();
                //else if (m_materials.Count == sides)
                //    return m_materials.ToArray();
                //else {
                //    for (int i = m_materials.Count; i < sides; i++)
                //        m_materials.Add(new SOPObjectMaterial(i, sop));
                //    return m_materials.ToArray();
                //}
                IObjectMaterial[] rets = new IObjectMaterial[getNumberOfSides(sop)];

                for (int i = 0; i < rets.Length; i++) {
                    rets[i] = new SOPObjectMaterial(i, sop);
                }

                return rets;
            }
        }

        /// <summary>
        /// Fixed so it updates properly
        /// </summary>
        public Vector3 Scale
        {
            get { return GetSOP().Scale; }
            set
            {
                if (CanEdit()) {
                    SceneObjectPart sop = GetSOP();
                    sop.Scale = value;
                    if (sop.ParentGroup != null) {
                        sop.ParentGroup.HasGroupChanged = true;
                        sop.ScheduleFullUpdate();
                    }
                }
            }
        }

        /// <summary>
        /// Updated by John McCaffery
        /// </summary>
        public Quaternion WorldRotation {
            get { return GetSOP().RotationOffset; }
            set {
                if (CanEdit()) {
                    SceneObjectPart sop = GetSOP();
                    if (sop.IsRoot)
                        sop.UpdateRotation(value);
                    else {
                        SceneObjectGroup group = m_rootScene.GetSceneObjectGroup(sop.GroupID);
                        sop.UpdateRotation(group.GroupRotation / value);
                    }
                }
            }
        }

        public Quaternion OffsetRotation
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public Vector3 WorldPosition
        {
            get { return GetSOP().AbsolutePosition; }
            set
            {
                if (CanEdit()) {
                    SceneObjectPart sop = GetSOP();
                    if (sop.IsRoot && sop.ParentGroup != null && sop.ParentGroup.IsAttachment) {
                        sop.AttachedPos = value;
                        sop.ParentGroup.HasGroupChanged = true;
                        sop.ScheduleFullUpdate();
                    } else if (sop.IsRoot)
                        sop.UpdateGroupPosition(value);
                    else
                        sop.UpdateOffSet(sop.GroupPosition - value);
                }
            }
        }

        public Vector3 OffsetPosition
        {
            get {
                SceneObjectPart sop = GetSOP();
                if (sop.ParentID == 0) 
                    return sop.AbsolutePosition;
                else                    
                    if (sop.ParentGroup.IsAttachment && sop.IsRoot)
                        return  sop.AttachedPos;
                    else 
                        return  sop.OffsetPosition; 
            }
            set
            {
                if (CanEdit())
                {
                    GetSOP().OffsetPosition = value;
                }
            }
        }

        public Vector3 SitTarget
        {
            get { return GetSOP().SitTargetPosition; }
            set 
            { 
                if (CanEdit())
                {
                    GetSOP().SitTargetPosition = value;
                }
            }
        }

        public string SitTargetText
        {
            get { return GetSOP().SitName; }
            set 
            { 
                if (CanEdit())
                {
                    GetSOP().SitName = value;
                }
            }
        }

        public string TouchText
        {
            get { return GetSOP().TouchName; }
            set 
            {
                if (CanEdit())
                {
                    GetSOP().TouchName = value;
                }
            }
        }

        public string Text
        {
            get { return GetSOP().Text; }
            set 
            {
                if (CanEdit())
                {
                    GetSOP().SetText(value,new Vector3(1.0f,1.0f,1.0f),1.0f);
                }
            }
        }

        public bool IsAttachment {
            get { return GetSOP().ParentGroup.IsAttachment; }
        }

        public bool IsRotationLockedX
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsRotationLockedY
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsRotationLockedZ
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsSandboxed
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsImmotile
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsAlwaysReturned
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsTemporary
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsFlexible
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public PhysicsMaterial PhysicsMaterial
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public IObjectPhysics Physics
        {
            get { return this; }
        }

        public IObjectShape Shape
        {
            get { return this; }
        }

        public IObjectInventory Inventory 
        {
            get { return new SOPObjectInventory(m_rootScene, GetSOP().TaskInventory, this); }
        }
        
        #region Public Functions

        public void Say(string msg) {
            Say(msg, 0, ChatTargetEnum.User);
        }

        public void Say(string msg,int channel)
        {
            Say(msg, channel, ChatTargetEnum.User);
        }

        public void Say(string msg, int channel, ChatTargetEnum target) {
            if (!CanEdit() || !Exists)
                return;
            SceneObjectPart sop = GetSOP();
            switch (target) {
                case ChatTargetEnum.User:
                    m_rootScene.SimChat(Utils.StringToBytes(msg), ChatTypeEnum.Say, channel, sop.AbsolutePosition, sop.Name, sop.UUID, false); 
                    break;
                case ChatTargetEnum.LSL:
                    m_rootScene.SimChatBroadcast(Utils.StringToBytes(msg), ChatTypeEnum.Region, channel, sop.AbsolutePosition, sop.Name, sop.UUID, false); 
                    break;
                case ChatTargetEnum.MRM:
                    m_rootScene.SimChat(Utils.StringToBytes(msg), ChatTypeEnum.Region, channel, sop.AbsolutePosition, sop.Name, sop.UUID, false); 
                    break;
            }
        }

        public void Say(string msg, int channel, bool broadcast, MRMChatTypeEnum type) {
            if (!CanEdit())
                return;
            ChatTypeEnum mrmType;
            switch (type) {
                case MRMChatTypeEnum.Broadcast: mrmType = ChatTypeEnum.Broadcast; break;
                case MRMChatTypeEnum.DebugChannel: mrmType = ChatTypeEnum.DebugChannel; break;
                case MRMChatTypeEnum.Owner: mrmType = ChatTypeEnum.Owner; break;
                case MRMChatTypeEnum.Region: mrmType = ChatTypeEnum.Region; break;
                case MRMChatTypeEnum.Say: mrmType = ChatTypeEnum.Say; break;
                case MRMChatTypeEnum.Whisper: mrmType = ChatTypeEnum.Whisper; break;
                default: mrmType = ChatTypeEnum.Say; break;
            }

            SceneObjectPart sop = GetSOP();
            if (broadcast)
                m_rootScene.SimChatBroadcast(Utils.StringToBytes(msg), mrmType, channel, sop.AbsolutePosition, sop.Name, sop.UUID, false);
            else
                m_rootScene.SimChat(Utils.StringToBytes(msg), mrmType, channel, sop.AbsolutePosition, sop.Name, sop.UUID, false); 
        }
         
        public void Dialog(UUID avatar, string message, string[] buttons, int chat_channel)
        {
            if (!CanEdit())
                return;

            IDialogModule dm = m_rootScene.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return;

            if (buttons.Length < 1)
            {
                Say("ERROR: No less than 1 button can be shown",2147483647);
                return;
            }
            if (buttons.Length > 12)
            {
                Say("ERROR: No more than 12 buttons can be shown",2147483647);
                return;
            }

            foreach (string button in buttons)
            {
                if (button == String.Empty)
                {
                    Say("ERROR: button label cannot be blank",2147483647);
                    return;
                }
                if (button.Length > 24)
                {
                    Say("ERROR: button label cannot be longer than 24 characters",2147483647);
                    return;
                }
            }

            dm.SendDialogToUser(
                avatar, GetSOP().Name, m_id, GetSOP().OwnerID,
                message, new UUID("00000000-0000-2222-3333-100000001000"), chat_channel, buttons);
            
        }
        
        #endregion


        #region Supporting Functions

        // Helper functions to understand if object has cut, hollow, dimple, and other affecting number of faces
        private static void hasCutHollowDimpleProfileCut(int primType, PrimitiveBaseShape shape, out bool hasCut, out bool hasHollow,
            out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == (int)PrimType.Box
                ||
                primType == (int)PrimType.Cylinder
                ||
                primType == (int)PrimType.Prism)

                hasCut = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0);
            else
                hasCut = (shape.PathBegin > 0) || (shape.PathEnd > 0);

            hasHollow = shape.ProfileHollow > 0;
            hasDimple = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // is it the same thing?

        }

        private static int getScriptPrimType(PrimitiveBaseShape primShape)
        {
            if (primShape.SculptEntry)
                return (int) PrimType.Sculpt;
            if (primShape.ProfileShape == ProfileShape.Square)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Box;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Tube;
            }
            else if (primShape.ProfileShape == ProfileShape.Circle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Cylinder;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Torus;
            }
            else if (primShape.ProfileShape == ProfileShape.HalfCircle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Curve1 || primShape.PathCurve == (byte) Extrusion.Curve2)
                    return (int) PrimType.Sphere;
            }
            else if (primShape.ProfileShape == ProfileShape.EquilateralTriangle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Prism;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Ring;
            }
            return (int) PrimType.NotPrimitive;
        }

        private static PrimitiveBaseShape getScriptPrimType(PrimType shape, PrimitiveBaseShape oldShape) {
            //Set the profile shape
            if (shape == PrimType.Box || shape == PrimType.Tube)
                oldShape.ProfileShape = ProfileShape.Square;
            else if (shape == PrimType.Cylinder || shape == PrimType.Torus)
                oldShape.ProfileShape = ProfileShape.Circle;
            else if (shape == PrimType.Sphere)
                oldShape.ProfileShape = ProfileShape.HalfCircle;
            else if (shape == PrimType.Prism || shape == PrimType.Ring)
                oldShape.ProfileShape = ProfileShape.EquilateralTriangle;

            //Set the curve
            if (shape == PrimType.Box || shape == PrimType.Cylinder || shape == PrimType.Prism)
                oldShape.PathCurve = (byte)Extrusion.Straight;
            else
                oldShape.PathCurve = (byte) Extrusion.Curve1;
            return oldShape;
        }

        private static int getNumberOfSides(SceneObjectPart part)
        {
            int ret;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            int primType = getScriptPrimType(part.Shape);
            hasCutHollowDimpleProfileCut(primType, part.Shape, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                default:
                case (int) PrimType.Box:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Cylinder:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Prism:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Sphere:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow)
                        ret += 1; // GOTCHA: LSL shows 2 additional sides here. 
                                  // This has been fixed, but may cause porting issues.
                    break;
                case (int) PrimType.Torus:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Tube:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Ring:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Sculpt:
                    ret = 1;
                    break;
            }
            return ret;
        }


        #endregion

        #region IObjectPhysics

        public bool Enabled
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool Phantom
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool PhantomCollisions
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public double Density
        {
            get { return (GetSOP().PhysActor.Mass/Scale.X*Scale.Y/Scale.Z); }
            set { throw new NotImplementedException(); }
        }

        public double Mass
        {
            get { return GetSOP().PhysActor.Mass; }
            set { throw new NotImplementedException(); }
        }

        public double Buoyancy
        {
            get { return GetSOP().PhysActor.Buoyancy; }
            set { GetSOP().PhysActor.Buoyancy = (float)value; }
        }

        public Vector3 GeometricCenter
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.GeometricCenter;
                return tmp;
            }
        }

        public Vector3 CenterOfMass
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.CenterOfMass;
                return tmp;
            }
        }

        public Vector3 RotationalVelocity
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.RotationalVelocity;
                return tmp;
            }
            set
            {
                if (!CanEdit())
                    return;

                GetSOP().PhysActor.RotationalVelocity = value;
            }
        }

        public Vector3 Velocity
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.Velocity;
                return tmp;
            }
            set
            {
                if (!CanEdit())
                    return;

                GetSOP().PhysActor.Velocity = value;
            }
        }

        public Vector3 Torque
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.Torque;
                return tmp;
            }
            set
            {
                if (!CanEdit())
                    return;

                GetSOP().PhysActor.Torque = value;
            }
        }

        public Vector3 Acceleration
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.Acceleration;
                return tmp;
            }
        }

        public Vector3 Force
        {
            get
            {
                Vector3 tmp = GetSOP().PhysActor.Force;
                return tmp;
            }
            set
            {
                if (!CanEdit())
                    return;

                GetSOP().PhysActor.Force = value;
            }
        }

        public bool FloatOnWater
        {
            set
            {
                if (!CanEdit())
                    return;
                GetSOP().PhysActor.FloatOnWater = value;
            }
        }

        public void AddForce(Vector3 force, bool pushforce)
        {
            if (!CanEdit())
                return;

            GetSOP().PhysActor.AddForce(force, pushforce);
        }

        public void AddAngularForce(Vector3 force, bool pushforce)
        {
            if (!CanEdit())
                return;

            GetSOP().PhysActor.AddAngularForce(force, pushforce);
        }

        public void SetMomentum(Vector3 momentum)
        {
            if (!CanEdit())
                return;

            GetSOP().PhysActor.SetMomentum(momentum);
        }

        #endregion

        #region Implementation of IObjectShape

        private UUID m_sculptMap = UUID.Zero;

        public UUID SculptMap
        {
            get { return m_sculptMap; }
            set
            {
                if (!CanEdit())
                    return;

                m_sculptMap = value;
                SetPrimitiveSculpted(SculptMap, (byte) SculptType);
            }
        }

        private SculptType m_sculptType = Object.SculptType.Default;

        public SculptType SculptType
        {
            get { return m_sculptType; }
            set
            {
                if (!CanEdit())
                    return;

                m_sculptType = value;
                SetPrimitiveSculpted(SculptMap, (byte) SculptType);
            }
        }

        public HoleShape HoleType
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public double HoleSize
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public PrimType PrimType
        {
            get { return (PrimType)getScriptPrimType(GetSOP().Shape); }
            set {
                GetSOP().Shape = getScriptPrimType(value, GetSOP().Shape); 
            }
        }

        private void SetPrimitiveSculpted(UUID map, byte type)
        {
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock();

            SceneObjectPart part = GetSOP();

            UUID sculptId = map;

            shapeBlock.ObjectLocalID = part.LocalId;
            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 150;

            // retain pathcurve
            shapeBlock.PathCurve = part.Shape.PathCurve;

            part.Shape.SetSculptProperties((byte)type, sculptId);
            part.Shape.SculptEntry = true;
            part.UpdateShape(shapeBlock);
        }


        #endregion
        
        #region Implementation of IObjectSound

        public IObjectSound Sound
        {
            get { return this; }
        }

        public void Play(UUID asset, double volume)
        {
            if (!CanEdit())
                return;

            GetSOP().SendSound(asset.ToString(), volume, true, 0, 0, false, false);
        }

        #endregion
    }
}
