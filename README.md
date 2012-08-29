XMRM
====

An extension of OpenSim's MRM capability to allow scripts to be written externally and loaded in world.

XMRMs (External Region Modules) are an extension of the MRM API which is built into OpenSim. The MRM API exposes common scripting functionality (primarily interacting with primitives) as a C# API. It is more powerful than traditional LSL style scripting as it can affect any object in the scene graph. XMRMs further extend this functionality by allowing code to be written externally, compiled to a DLL and then loaded in world. This code can then be recompiled and reloaded without ever restarting the virtual world.
