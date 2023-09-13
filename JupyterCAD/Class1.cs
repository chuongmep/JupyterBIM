using Autodesk.AutoCAD.Runtime;

namespace JupyterCAD;


public class Class1
{
    [CommandMethod("DebugTraceTest")]
    public void Test()
    {
        // doc
        var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
        doc.Editor.WriteMessage("Hello World!");
    }
}