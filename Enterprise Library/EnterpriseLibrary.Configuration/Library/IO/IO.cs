using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Schalltech.EnterpriseLibrary.IO
{
    public class Path
    {
        static public string GetAbsolutePath(string path)
        {
            Uri uri;
            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out uri))
            {
                throw new Exception(string.Format("'{0}' is not a valid URI", path));
            }
            else if (!uri.IsAbsoluteUri)
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, path));
            }
            else
            {
                return path;
            }
        }

        static public string CheckHintPaths(string[] hint_paths)
        {
            string path = null;

            if (hint_paths != null)
            {
                foreach (var hint_path in hint_paths)
                {
                    if (!string.IsNullOrEmpty(hint_path))
                    {
                        try
                        {
                            path = IO.Path.GetAbsolutePath(hint_path);

                            if (System.IO.File.Exists(path))
                                break;
                            else
                                path = null;
                        }
                        catch (Exception)
                        { }
                    }
                }
            }

            return path;
        }
    }
}
