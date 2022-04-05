using QuickLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace quick_picture_viewer
{
    public class QoiWrapper : TypeWrapper
    {
        public QoiWrapper()
        {
            TypeName = "QOI";
            ShowTypeOps = false;
        }

        public override FileTypeMan.OpenResult Open(string path)
        {
            try
            {
                byte[] rawQoi = File.ReadAllBytes(path);
                Bitmap bitmap = QoiEngine.Decode(rawQoi);
                    return new FileTypeMan.OpenResult
                    {
                        Bmp = bitmap
                    };
            }
            catch
            {
                return new FileTypeMan.OpenResult
                {
                    ErrorMessage = LangMan.Get("unable-open-file") + ": " + Path.GetFileName(path)
                };
            }
        }
    }
}
