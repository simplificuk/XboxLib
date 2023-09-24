using System.Collections;
using System.Collections.Generic;

namespace XboxIsoLib
{
    public class XIsoNode: IEnumerable<XIsoNode>
    {
        public XboxIso Iso { get; internal set; }
        public bool Root { get; set; }
        public XIsoNode Parent { get; set; }
        public string Name { get; set; }
        public string FullPath { get {
                if (Root) return Name.Trim('/');
                return Parent.FullPath + "/" +  Name.Trim('/');
            } }
        public long Position { get; set; }
        public long Length { get; set; }
        public XIsoAttributes Attributes { get; set; }
        public List<XIsoNode> Children { get; set; }
        public bool IsDirectory {  get { return Attributes.HasFlag(XIsoAttributes.Directory); }}
        public byte[] Data { get { return Iso.Read(this); } }

        public XIsoNode(): this(false)
        {
        }

        internal XIsoNode(bool root)
        {
            Root = root;
            if (root)
            {
                Name = "/";
                Attributes = XIsoAttributes.Directory;
            }
            Children = new List<XIsoNode>();
        }

        internal XIsoNode(XIsoNode parent, string name, long pos, long len, XIsoAttributes attrs): this(false)
        {
            if (parent != null)
            {
                Iso = parent.Iso;
                parent.Children.Add(this);
            }
            Parent = parent;
            // Fix for xbox FTP clients reading it as an absolute path
            if (name.StartsWith("/"))
                name = name.Substring(1);
            Name = name;
            Position = pos;
            Length = len;
            Attributes = attrs;
        }

        public IEnumerator<XIsoNode> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        public string ToString()
        {
            return FullPath;
        }
    }
}