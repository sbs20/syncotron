using System;

namespace Sbs20.Data
{
    [Flags]
    public enum DbTextParameterFlag
    {
        None = 0,
        AllowEmpty = 1 << 0,
        Unicode = 1 << 1
    }
}
