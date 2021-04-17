﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace NodeController.Patches
{
    public static class NetManagerPatches
    {
        public static IEnumerable<CodeInstruction> SimulationStepImplTranspiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var enumerator = instructions.GetEnumerator();

            var ldLoc1Found = false;
            var brFalseLabel = default(Label);

            while(enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                if (instruction.opcode == OpCodes.Ldloc_1)
                {
                    ldLoc1Found = true;
                    yield return instruction;
                }
                else if (ldLoc1Found && instruction.opcode == OpCodes.Brfalse)
                {
                    brFalseLabel = (Label)instruction.operand;
                    yield return instruction;
                    break;
                }
                else
                {
                    ldLoc1Found = false;
                    yield return instruction;
                }
            }

            while (enumerator.MoveNext())
            {
                var instruction = enumerator.Current;
                if(instruction.labels.Contains(brFalseLabel))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Manager), nameof(Manager.SimulationStep)));

                yield return instruction;
            }
        }
    }
}