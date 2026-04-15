using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TypePicker
{
    public class TypePicker : ResoniteMod
    {
        internal const string VERSION_CONSTANT = "2.0.4";
        public override string Name => "TypePicker";
        public override string Author => "ForgottenSin(InfernoEye)"; // Originaly by TheJebForge. Ported by art0007i :)
        public override string Version => VERSION_CONSTANT;
        public override string Link => "https://github.com/InfernoEye/TypePicker";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"InfernoEye.TypePicker");
            harmony.PatchAll();
        }

        public static void BuildComponentSelectorUI(ComponentSelector selector, object uiDisplayClass)
        {
            try
            {
                var ui = Traverse.Create(uiDisplayClass).Field<UIBuilder>("ui").Value;
                var fields = selector.GetSyncMember("_customGenericArguments") as SyncRefList<TextField>;
                if (fields == null) return;

                var refField = selector.FindNearestParent<Slot>()?.GetComponentOrAttach<ReferenceField<IWorldElement>>();
                if (refField == null) return;

                var paramIndexComponent = selector.FindNearestParent<Slot>()?.GetComponentOrAttach<ValueField<ushort>>();
                if (paramIndexComponent == null) return;

                paramIndexComponent.Value.Value = 0;

                selector.RunInUpdates(1, () =>
                {
                    if (fields.Count <= 1) return;
                    for (int i = 0; i < fields.Count; i++)
                    {
                        var f = fields[i];
                        if (f?.Slot?.Parent?.Parent == null) continue;

                        var textSlot = f.Slot.Parent.Parent[0][0];
                        if (textSlot == null) continue;

                        var btn = textSlot.AttachComponent<Button>();
                        var text = textSlot.GetComponent<Text>();
                        if (text != null)
                            btn.SetupBackgroundColor(text.Color);

                        var colorDriver = btn.ColorDrivers.FirstOrDefault();
                        if (colorDriver != null)
                            colorDriver.DisabledColor.Value = RadiantUI_Constants.Hero.GREEN;

                        var radio = textSlot.AttachComponent<ValueRadio<ushort>>();
                        radio.OptionValue.Value = (ushort)i;
                        radio.TargetValue.Target = paramIndexComponent.Value;

                        var bvd = textSlot.AttachComponent<BooleanValueDriver<bool>>();
                        bvd.FalseValue.Value = true;
                        bvd.TrueValue.Value = false;
                        bvd.TargetField.Target = btn.EnabledField;
                        radio.CheckVisual.Target = bvd.State;
                    }
                });

                SyncMemberEditorBuilder.Build(refField.Reference, "Type picker", null, ui);
                ui.HorizontalLayout(8f);
                {
                    ui.Button("Base type").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], FindBaseType(refField));
                    ui.Button("Inner type").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], FindInnerType(refField));
                }
                ui.NestOut();

                ui.Text("Cast to:");
                ui.HorizontalLayout(8f);
                {
                    ui.Button("SyncRef").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], CastToSyncRef(refField));
                    ui.Button("SyncRef Inner").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], CastToSyncRefInner(refField));
                    ui.Button("IField").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], CastToIField(refField));
                    ui.Button("IField Inner").LocalPressed += (button, data) => SetType(fields[paramIndexComponent.Value.Value], CastToIFieldInner(refField));
                }
                ui.NestOut();
            }
            catch (Exception ex)
            {
                Error($"TypePicker BuildComponentSelectorUI error: {ex}");
            }
        }

        static Type FindBaseType(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var target = refField.Reference.Target;
                if (target == null) return null;

                var type = target.GetType();
                return type;
            }
            catch { return null; }
        }

        static Type FindInnerType(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var target = refField.Reference.Target;
                if (target == null) return null;

                var type = target.GetType();

                if (type.IsGenericType)
                {
                    var genericDef = type.GetGenericTypeDefinition();
                    if (genericDef == typeof(Sync<>) || (type.BaseType?.IsGenericType == true && type.BaseType.GetGenericTypeDefinition() == typeof(SyncField<>)))
                    {
                        var innerType = type.GenericTypeArguments[0];
                        return innerType;
                    }
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IField<>))
                    {
                        var innerType = iface.GenericTypeArguments[0];
                        return innerType;
                    }
                }

                Warn($"FindInnerType - No inner type found for {type.FullName}");
            }
            catch (Exception ex)
            {
                Error($"FindInnerType error - {ex.Message}");
            }
            return null;
        }

        static Type CastToSyncRef(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var target = refField.Reference.Target;
                if (target is ISyncRef syncRef)
                {
                    var targetType = syncRef.TargetType;
                    if (targetType != null)
                    {
                        var result = typeof(SyncRef<>).MakeGenericType(targetType);
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"CastToSyncRef error - {ex.Message}");
            }
            return null;
        }

        static Type CastToSyncRefInner(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var syncRefType = CastToSyncRef(refField);
                if (syncRefType != null && syncRefType.IsGenericType)
                {
                    var innerType = syncRefType.GenericTypeArguments[0];
                    return innerType;
                }
            }
            catch (Exception ex)
            {
                Error($"CastToSyncRefInner error - {ex.Message}");
            }
            return null;
        }

        static Type CastToIField(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var target = refField.Reference.Target;
                if (target is IField field)
                {
                    var valueType = field.ValueType;
                    if (valueType != null)
                    {
                        var result = typeof(IField<>).MakeGenericType(valueType);
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Error($"CastToIField error - {ex.Message}");
            }
            return null;
        }

        static Type CastToIFieldInner(ReferenceField<IWorldElement> refField)
        {
            try
            {
                var iFieldType = CastToIField(refField);
                if (iFieldType != null && iFieldType.IsGenericType)
                {
                    var innerType = iFieldType.GenericTypeArguments[0];
                    return innerType;
                }
            }
            catch (Exception ex)
            {
                Error($"CastToIFieldInner error - {ex.Message}");
            }
            return null;
        }

        static void SetType(TextField field, Type type)
        {
            try
            {
                if (field.Editor.Target?.Text.Target != null && type != null)
                {
                    string typeString = field.World.Types.EncodeType(type);

                    if (string.IsNullOrEmpty(typeString))
                    {
                        typeString = type.GetNiceName();
                    }

                    field.Editor.Target.Text.Target.Text = typeString;
                }
            }
            catch (Exception ex)
            {
                Error($"SetType error - {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(ComponentSelector), "BuildUI")]
        class ComponentSelector_BuildUI_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int startIndex = -1;
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

                for (int i = 0; i < codes.Count; i++)
                {
                    CodeInstruction instr = codes[i];
                    if (instr.opcode == OpCodes.Ldstr && ((string)instr.operand).Contains("ComponentSelector.CustomGenericArguments"))
                    {
                        startIndex = i - 1;
                        break;
                    }
                }

                if (startIndex > -1)
                {
                    MethodInfo method = typeof(TypePicker).GetMethod("BuildComponentSelectorUI", BindingFlags.Public | BindingFlags.Static);
                    codes.InsertRange(startIndex, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, method)
                    });
                }
                else
                {
                    Warn("Could not find patch target! Mod won't work.");
                }

                return codes.AsEnumerable();
            }
        }
    }
}