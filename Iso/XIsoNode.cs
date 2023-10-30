using System.Collections;
using System.Collections.Generic;

namespace XboxLib.Iso
{
    public class XIsoNode: IEnumerable<XIsoNode>
    {
        public XIso Iso { get; internal set; }
        public bool Root { get; set; }
        public XIsoNode Parent { get; set; }
        public string Name { get; set; }
        public string FullPath { get {
                if (Root || Parent.Root) return Name.Trim('/');
                return Parent.FullPath + "/" +  Name.Trim('/');
            } }
        public long Position { get; set; }
        public long Length { get; set; }
        public XIsoAttribute Attributes { get; set; }
        public List<XIsoNode> Children { get; set; }
        public bool IsDirectory => Attributes.HasFlag(XIsoAttribute.Directory);
        public byte[] Data => Iso.Read(this);

        public XIsoNode()
        {
            Children = new List<XIsoNode>();
        }

        internal XIsoNode(XIsoNode parent, string name, long pos, long len, XIsoAttribute attrs): this()
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

        public override string ToString()
        {
            return FullPath;
        }
    }
}