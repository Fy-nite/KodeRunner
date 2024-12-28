using Wasmtime;
using System;
using System.IO;
using System.Text;
using KodeRunner;

namespace KodeRunner
{
    public class WasmRunner
    {
        public static void Run(string wasmFile, string functionName, string[] args)
        {
            var engine = new Engine();
            var module = Module.FromFile(engine, wasmFile);


            using var linker = new Linker(engine);
            using var store = new Store(engine);

   

            var instance = linker.Instantiate(store, module);
            var run = instance.GetAction(functionName)!;
            run();

        }
    }
}