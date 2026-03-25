using System;
using System.Windows.Forms;

#nullable enable

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new DeckEvaluatorForm());
    }
}
