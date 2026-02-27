using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace DLS.Simulation
{
	public class SimPin
	{
		public readonly int ID;
		public readonly SimChip parentChip;
		public readonly bool isInput;
        public ulong State;

        public SimPin[] ConnectedTargetPins = Array.Empty<SimPin>();

		// Simulation frame index on which pin last received an input
		public int lastUpdatedFrameIndex;

		// Address of pin from where this pin last received its input
		public int latestSourceID;
		public int latestSourceParentChipID;

		public List<ulong> Sources = new List<ulong> {};
        public List<ulong> SourceStates = new List<ulong> { };

        // Number of wires that input their signal to this pin.
        // (In the case of conflicting signals, the pin chooses randomly)
        public int numInputConnections;
		public int numInputsReceivedThisFrame;

		public SimPin(int id, bool isInput, SimChip parentChip)
		{
			this.parentChip = parentChip;
			this.isInput = isInput;
			ID = id;
			latestSourceID = -1;
			latestSourceParentChipID = -1;

			PinState.SetAllDisconnected(ref State);
        }

		public bool FirstBitHigh => PinState.FirstBitHigh(State);

		public void PropagateSignal()
		{
			int length = ConnectedTargetPins.Length;
			for (int i = 0; i < length; i++)
			{
				ConnectedTargetPins[i].ReceiveInput(this);
			}
        }

		// Called on sub-chip input pins, or chip dev-pins
		void ReceiveInput(SimPin source)
		{
			// If this is the first input of the frame, reset the received inputs counter to zero
			if (lastUpdatedFrameIndex != Simulator.simulationFrame)
			{
				lastUpdatedFrameIndex = Simulator.simulationFrame;
				numInputsReceivedThisFrame = 0;
			}

			//bool set;
			ulong initialState = State;
			//if (numInputsReceivedThisFrame > 0)
			//{
			//	// Has already received input this frame, so choose at random whether to accept conflicting input.
			//	// Note: for multi-bit pins, this choice is made identically for all bits, rather than individually.
			//	// Todo: maybe consider changing to per-bit in the future...)

			//	ulong OR = source.State | State;
			//	ulong AND = source.State & State;
			//	uint bitsNew = (uint)(Simulator.RandomBool() ? OR : AND); // randomly accept or reject conflicting state

			//	uint mask = (uint)(OR >> 32); // tristate flags
			//	bitsNew = (uint)((bitsNew & ~mask) | ((uint)OR & mask)); // can always accept input for tristated bits

			//	uint tristateNew = (uint)(AND >> 32);
			//	ulong stateNew = (ulong)(bitsNew | (tristateNew << 32));
			//	set = stateNew != State;
			//	State = stateNew;
			//}
			//else
			//{
			//	// First input source this frame, so accept it.
			//	if (State != source.State) parentChip.updatedThisTick = true;
			//	State = source.State;
			//	//Debug.Log("State: " + State);
			//	set = true;
			//}

			latestSourceID = source.ID;
			latestSourceParentChipID = source.parentChip.ID;
			ulong sourceUniqueID = ((ulong)(uint)source.ID) | (((ulong)(uint)source.parentChip.ID) << 32);
			if (!(Sources.Contains(sourceUniqueID)))
			{
				Sources = Sources.Append(sourceUniqueID).ToList();
				SourceStates = SourceStates.Append(source.State).ToList();
				//Debug.Log(source.ID + " To " + ID);
			}
			else
			{
				SourceStates[Sources.IndexOf(sourceUniqueID)] = source.State;
                //if (Sources.Count > 1) Debug.Log(sourceUniqueID + " H set " + source.State);
            }
			if ((uint)(source.State >> 32) == 0 || Sources.Count == 1) 
			{
                State = source.State;
            }
			else
			{
				if (Sources.Count >= 2)
				{
					//Debug.Log(Sources.Count + " C");
					ulong MultiInputState = 0;
                    PinState.SetAllDisconnected(ref MultiInputState);
                    for (int i = 0; i < Sources.Count; i++)
					{
						//Debug.Log(MultiInputState + " G");
                        //Debug.Log(SourceStates[i] + " I");
                        ulong OR = SourceStates[i] | MultiInputState;
						ulong AND = SourceStates[i] & MultiInputState;
						uint bitsNew = (uint)OR; // randomly accept or reject conflicting state

						uint mask = (uint)(OR >> 32); // tristate flags
						bitsNew = (uint)((bitsNew & ~mask) | ((uint)OR & mask)); // can always accept input for tristated bits
                        //Debug.Log(bitsNew + " F");
                        uint tristateNew = (uint)(AND >> 32);
                        MultiInputState = (ulong)(bitsNew | (((ulong)tristateNew) << 32));
						//Debug.Log(SourceStates[i] + " E");
					}
                    State = MultiInputState;
					//Debug.Log(MultiInputState + " D");
				}
			}
			if(initialState != State) parentChip.updatedThisTick = true;
			//Debug.Log(Sources.Count + " B");

			numInputsReceivedThisFrame++;

			// If this is a sub-chip input pin, and has received all of its connections, notify the sub-chip that the input is ready
			if (isInput && numInputsReceivedThisFrame == numInputConnections)
			{
				parentChip.numInputsReady++;
			}
		}
    }
}