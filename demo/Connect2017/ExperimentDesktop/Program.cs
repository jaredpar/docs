using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExperimentDesktop
{
    public class Program
    {
        static void Main(string[] args)
        {
            Span<int> s = new [] { 1, 2, 3 };
            object o = s;
            Console.WriteLine(s.Length);
        }
    }
}
