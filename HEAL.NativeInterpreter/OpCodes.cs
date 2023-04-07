namespace HEAL.NativeInterpreter {
  public enum OpCode : int {
    None = 0,
    Add = 1,
    Sub = 2,
    Mul = 3,
    Div = 4,
    Sin = 5,
    Cos = 6,
    Tan = 7,
    Log = 8,
    Exp = 9,
    Variable = 18,
    Number = 20,
    Power = 22,
    Root = 23,
    Square = 28,
    Sqrt = 29,
    Abs = 48,
    AQ = 49,
    Cube = 50,
    Cbrt = 51,
    Tanh = 52,
    Constant = 54
  };
}
