// Заглушка UnityEngine.CoreModule — только для компиляции.
// ВАЖНО: вид члена должен совпадать с реальным UnityEngine, иначе IL не резолвится.

namespace UnityEngine
{
    public class Object
    {
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
    }

    public static class Time
    {
        // Реально Time.unscaledTime — статическое СВОЙСТВО, не поле.
        public static float unscaledTime { get; }
    }
}
