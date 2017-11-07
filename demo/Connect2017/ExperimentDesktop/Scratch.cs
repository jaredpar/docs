using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExperimentDesktop.Scratch
{
    public readonly ref struct Span<T>
    {
        private readonly ref T _pointer;
        private readonly int _length;

        public ref T this[int index] => ...
    }
}
