using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Synthesis.Bethesda.GUI
{
    public class EnumDictionarySettingsVM : ADictionarySettingsVM
    {
        public EnumDictionarySettingsVM(string memberName, KeyValuePair<string, SettingsNodeVM>[] values, SettingsNodeVM prototype)
            : base(memberName, values, prototype)
        {
        }

        public static EnumDictionarySettingsVM Factory(SettingsParameters param, string memberName, Type enumType, Type valType, object? defaultVals)
        {
            var vals = GetDefaultValDictionary(defaultVals);
            var proto = SettingsNodeVM.MemberFactory(param, member: null, targetType: valType, defaultVal: null);
            proto.WrapUp();
            return new EnumDictionarySettingsVM(
                memberName,
                Enum.GetNames(enumType).Select(e =>
                {
                    if (!vals.TryGetValue(e, out var defVal))
                    {
                        defVal = null;
                    }
                    return new KeyValuePair<string, SettingsNodeVM>(
                        e,
                        SettingsNodeVM.MemberFactory(param, member: null, targetType: valType, defaultVal: defVal));
                }).ToArray(),
                proto);
        }

        public override SettingsNodeVM Duplicate()
        {
            return new EnumDictionarySettingsVM(MemberName, _values, _prototype);
        }
    }
}