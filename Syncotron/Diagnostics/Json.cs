namespace Sbs20.Syncotron.Diagnostics
{
    public class Json
    {
        public static string ToString(object o)
        {
            var s = Serialiserabler.ToSerialisable(o);
            return Newtonsoft.Json.JsonConvert.SerializeObject(s, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
