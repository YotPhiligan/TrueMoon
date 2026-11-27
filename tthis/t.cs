class Inner { public Inner(object o, System.Func<int,int,int> c, System.Action<int> s){} }
class Outer {
    private readonly Inner _i = new(this, static (a,b)=>a|b, Handler);
    private void Handler(int v) {}
}
