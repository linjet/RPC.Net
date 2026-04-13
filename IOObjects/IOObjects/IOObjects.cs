namespace IOObjects
{
    public static class HttpInfo
    {
        public static Dictionary<int, string> ResponseCodes = new Dictionary<int, string>
        {
            {    0, "Undefined" },
            {  100, "Continue" },
            {  200, "Success" },
            {  204, "No Content" },
            {  300, "Redirect" },
            {  301, "Moved Permanently" },
            {  303, "See Other" },
            {  307, "Temporary Redirect" },
            {  308, "Permanent Redirect" },
            {  400, "Bad request" },
            {  401, "Unauthorized" },
            {  403, "Forbidden" },
            {  404, "Not Found" },
            {  405, "Method Not Allowed" },
            {  406, "Not Acceptable" },
            {  407, "Proxy Authentication Required" },
            {  408, "Request Timeout" },
            {  423, "Locked" },
            {  429, "Too Many Requests" },
            {  500, "Internal Server Error" },
            {  503, "Service Unavailable" },
            { 1000, "Socket Error" }
        };
        public static Dictionary<int, string> MediaTypes = new Dictionary<int, string> { };
    }
    public class IOList : List<KeyValuePair<string, string>>
    {
        public IOList() { }
        private new void Add(KeyValuePair<string, string> Pair)
        {
            base.Add(Pair);
        }
        public string? Find(string Key)
        {
            KeyValuePair<string, string>? Pair = base.Find(x => x.Key.ToLower() == Key.Trim().ToLower());
            if (Pair == null || string.IsNullOrEmpty(Pair.Value.Value)) { return null; }
            return Pair.Value.Value;
        }
        public IOList Find(string Key, string ValuePart, bool CaseIgnore)
        {
            ValuePart = ValuePart.Trim();
            if (CaseIgnore) { ValuePart = ValuePart.ToLower(); }
            IOList Result = new IOList();
            List<KeyValuePair<string, string>> Finded = base.FindAll(x => x.Key.ToLower() == Key.Trim().ToLower());
            foreach (var Pair in Finded)
            {
                string Value = Pair.Value;
                if (CaseIgnore) { Value = Value.ToLower(); }
                if (!ValuePart.StartsWith('*') && !ValuePart.EndsWith('*')) // отбираем полное совпадение
                {
                    if (Value == ValuePart) { Result.Add(Pair); }
                    continue;
                }
                if (ValuePart.StartsWith('*') && !ValuePart.EndsWith('*')) // отбираем только совпадения вконце значения
                {
                    if (Value.EndsWith(ValuePart)) { Result.Add(Pair); }
                    continue;
                }
                if (!ValuePart.StartsWith('*') && ValuePart.EndsWith('*')) // отбираем только совпадения вначале значения
                {
                    if (Value.StartsWith(ValuePart)) { Result.Add(Pair); }
                    continue;
                }
                if (ValuePart.StartsWith('*') && ValuePart.EndsWith('*')) // отбираем только совпадения внутри, вначале или вконце значения
                {
                    if (Value.IndexOf(ValuePart) >= 0) { Result.Add(Pair); }
                }
                Result.Add(Pair);
            }
            return Result;
        }
        public IOList FindAll(string Key)
        {
            IOList Result = new IOList();
            List<KeyValuePair<string, string>> Finded = base.FindAll(x => x.Key.ToLower() == Key.Trim().ToLower());
            foreach (var Pair in Finded) { Result.Add(Pair); }
            return Result;
        }
        public void Add(string Key, string Value, bool dupallow)
        {
            KeyValuePair<string, string> Pair = new KeyValuePair<string, string>(Key.Trim(), Value.Trim());
            if (dupallow)
            {
                Add(Pair);
                return;
            }
            if (!string.IsNullOrEmpty(Find(Key))) { return; }
            Add(Pair);
        }
    }
}
