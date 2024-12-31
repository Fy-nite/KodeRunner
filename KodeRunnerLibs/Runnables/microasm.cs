using Python.Runtime;

namespace MicroASM
{
    class PYNETASMrunner
    {
        public void ASMRunner()
        {
            if (!PythonEngine.IsInitialized) // Since using asp.net, we may need to re-initialize
            {
                PythonEngine.Initialize();
                Py.GIL();
            }

            using (var scope = Py.CreateScope())
            {
                string file = "interp.py"; // The python file we want to run
                string code = File.ReadAllText(file); // Get the python file as raw text
                var scriptCompiled = PythonEngine.Compile(code, file); // Compile the code/file
                scope.Execute(scriptCompiled);
                PyObject MicroASM = scope.Get("RegisterMachine");
                // the function takes in a string
                PyObject[] args = new PyObject[] { new PyString(code) };
                PyObject pythongReturn = MicroASM.InvokeMethod("execute", args); // Run the python code
            }
        }
    }
}
