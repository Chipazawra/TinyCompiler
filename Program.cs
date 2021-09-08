using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using System.Linq;

namespace TinyCompiler
{
    class Program
    {
        static void Main(string[] args)
        {

            var compiler = new Compiler();

            //var p1 = compiler.pass1("[ x y z ] ( 2*3*x + 5*y - 3*z ) / (1 + 3 + 2*2)");
            var p1 = compiler.pass1("[ x y z ] x - y - z + 10 / 5 / 2 - 7 / 1 / 7");
            var p2 = compiler.pass2(p1);
            Console.WriteLine(JsonConvert.SerializeObject(p2));
            var c = compiler.pass3(p2);
            Console.WriteLine(c.Aggregate((workingSentence, next) => workingSentence + " " + next ));

            var r0 = simulate(c, new int[]{3}); 
        }

        public static int simulate(List<string> asm, int[] argv)
        {
            int r0 = 0;
            int r1 = 0;
            List<int> stack = new List<int>();
            foreach (string ins in asm)
            {
            string code = ins.Substring(0,2);
            switch (code)
            {
                case "IM": r0 = Int32.Parse(ins.Substring(2).Trim()); break;
                case "AR": r0 = argv[Int32.Parse(ins.Substring(2).Trim())]; break;
                case "SW": int tmp = r0; r0 = r1; r1 = tmp; break;
                case "PU": stack.Add(r0); break;
                case "PO": r0 = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1); break;
                case "AD": r0 += r1; break;
                case "SU": r0 -= r1; break;
                case "MU": r0 *= r1; break;
                case "DI": r0 /= r1; break;
            }
            }
            return r0;
        }

    }
    public class Compiler
    {
        List<string> var = new List<string>();
        Stack<String> stkOp = new Stack<string>();
        Stack<String> stkOps = new Stack<string>();
        Stack<Ast> stkAst = new Stack<Ast>();

        Action<string, Stack<Ast>> CompileOps = delegate (string Op, Stack<Ast> stk)
        {
            var b = stk.Pop();
            var a = stk.Pop();
            stk.Push(new BinOp(Op, a, b));
        };
        Ast RecursiveDescent(Ast node)
        {
            Ast ret;

            if (node.GetType() == typeof(BinOp))
            {
                if (node._a.GetType() == typeof(BinOp))
                {
                    node._a = RecursiveDescent(node._a);
                }

                if (node._b.GetType() == typeof(BinOp))
                {
                    node._b = RecursiveDescent(node._b);
                }

                if (node._a.GetType() == typeof(UnOp) &&
                    node._a._op == "imm" &&
                    node._b.GetType() == typeof(UnOp) &&
                    node._b._op == "imm")
                {
                    switch (node._op)
                    {
                        case "+":
                            return new UnOp("imm",
                            node._a._n +
                            node._b._n);

                        case "-":
                            return new UnOp("imm",
                            node._a._n -
                            node._b._n);

                        case "*":
                            return new UnOp("imm",
                            node._a._n *
                            node._b._n);

                        case "/":
                            return new UnOp("imm",
                            node._a._n /
                            node._b._n);

                        default: throw new FormatException();
                    }
                }

                ret = new BinOp(node._op, node._a, node._b);

            }
            else
            {
                ret = new UnOp(node._op, node._n);
            }

            return ret;
        }

        void RecursiveDescent(Ast node, List<string> code)
        {
            if (node.GetType() == typeof(BinOp))
            {
                RecursiveDescent(node._a, code);
                RecursiveDescent(node._b, code);

                switch (node._op)
                {

                    case "-":
                        {
                            code.Add($"PO");
                            code.Add($"SW");
                            code.Add($"PO");
                            code.Add($"SU");
                            code.Add($"PU"); break;
                        }
                    case "+":
                        {
                            code.Add($"PO");
                            code.Add($"SW");
                            code.Add($"PO");
                            code.Add($"AD"); 
                            code.Add($"PU"); break;
                        }
                    case "/":
                        {
                            code.Add($"PO");
                            code.Add($"SW");
                            code.Add($"PO");
                            code.Add($"DI"); 
                            code.Add($"PU"); break;
                        }
                    case "*":
                        {   
                            code.Add($"PO");
                            code.Add($"SW");
                            code.Add($"PO");
                            code.Add($"MU"); 
                            code.Add($"PU"); break;
                        }
                    default: throw new FormatException();
                }

            }
            else
            {
                switch (node._op)
                {
                    case "imm":
                        {
                            code.Add($"IM {node._n}"); break; 
                            
                        }
                    case "arg":
                        {
                            code.Add($"AR {node._n}"); break;
                        }
                    default: throw new FormatException();
                }

                code.Add($"PU");
            }

        }

        public Ast pass1(string prog)
        {
            List<string> tokens = tokenize(prog);

            var OpsTmpl = @"\+|\-|\/|\*|\(|\)";
            var OpTmpl = "[a-zA-Z]|[0-9]";
            var OpsOrder = new Dictionary<string, int> { { "(", -1 }, { ")", -1 }, { "+", 1 }, { "-", 1 }, { "/", 1 }, { "*", 2 } };

            foreach (string token in tokens)
            {
                if (token == "[")
                    stkOps.Push(token);
                else if (token == "]")
                    stkOps.Pop();
                else if (stkOps.Count > 0 && "[" == stkOps.Peek())
                    var.Add(token);
                else if (Regex.IsMatch(token, OpsTmpl))
                {
                    if (stkOps.Count == 0)
                        stkOps.Push(token);
                    else if (token == "(")
                        stkOps.Push(token);
                    else if (token == ")")
                    {
                        while (stkOps.Count > 0  && stkOps.Peek() != "(")
                        {
                            var PopOps = stkOps.Pop();
                            CompileOps(PopOps, stkAst);
                        }

                        stkOps.Pop();
                    }
                    else if (OpsOrder[token] <= OpsOrder[stkOps.Peek()])
                    {
                        while (stkOps.Count > 0  && stkOps.Peek() != "(")
                        {
                            var PopOps = stkOps.Pop();
                            CompileOps(PopOps, stkAst);
                        }
                        stkOps.Push(token);
                    }
                    else
                    {
                        stkOps.Push(token);
                    }
                }
                else if (Regex.IsMatch(token, OpTmpl))
                {
                    stkOp.Push(token);
                    if (Regex.IsMatch(token, "[0-9]"))
                        stkAst.Push(new UnOp("imm", int.Parse(token)));
                    else
                        stkAst.Push(new UnOp("arg", var.IndexOf(token)));
                }
                else
                {
                    throw new FormatException();
                }
            }

            while (stkOps.Count > 0)
            {
                var PopOps = stkOps.Pop();
                CompileOps(PopOps, stkAst);
            }

            return stkAst.Pop();
        }
        public Ast pass2(Ast ast)
        {
            return RecursiveDescent(ast);
        }
        public List<string> pass3(Ast ast)
        {
            List<String> code = new List<string>();
            RecursiveDescent(ast, code);
            code.Add($"PO");
            return code;
        }
        private List<string> tokenize(string input)
        {
            List<string> tokens = new List<string>();
            Regex rgxMain = new Regex("\\[|\\]|[-+*/=\\(\\)]|[A-Za-z_][A-Za-z0-9_]*|[0-9]*(\\.?[0-9]+)");
            MatchCollection matches = rgxMain.Matches(input);
            foreach (Match m in matches) tokens.Add(m.Groups[0].Value);
            return tokens;
        }
    }
    public class Ast
    {
        public string _op;
        public int _n;
        public Ast _a;
        public Ast _b;
        public string Op()
        {
            return _op;
        }

        public Ast a()
        {
            return _a;
        }

        public Ast b()
        {
            return _b;
        }

        public int n()
        {
            return _n;
        }
    }
    public class BinOp : Ast
    {
        public BinOp(string op, Ast a, Ast b)
        {
            _op = op;
            _a = a;
            _b = b;
        }

    }
    public class UnOp : Ast
    {
        public UnOp(string op, int n)
        {
            _op = op;
            _n = n;
        }
    }

}
