﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MapleManager.Controls;
using MapleManager.WzTools.Helpers;

namespace MapleManager.WzTools.Objects
{
    public class WzUOL : PcomObject
    {
        public string Path { get; set; }
        public bool Absolute { get; set; } = false;

        public override void Read(BinaryReader reader)
        {
            Debug.Assert(reader.ReadByte() == 0);
            Path = reader.ReadString(1, 0, 0);
        }

        public override void Write(ArchiveWriter writer)
        {
            writer.Write((byte)0);
            writer.Write(Path, 1, 0);
        }

        public override void Set(string key, object value)
        {
            if (value is string x) Path = x;
            else throw new InvalidDataException();
        }

        public override object Get(string key)
        {
            var obj = ActualObject();
            if (obj is PcomObject po) return po.Get(key);
            return obj;
        }

        public override void Rename(string key, string newName)
        {
            throw new NotImplementedException();
        }

        public string ActualPath()
        {
            var stk = new Stack<string>();
            var elems = (this.Parent.GetFullPath() + "/" + Path).Split('\\', '/');
            foreach (var elem in elems)
            {
                if (elem == "..") stk.Pop();
                else if (elem == ".") continue;
                else stk.Push(elem);
            }
            return string.Join("/", stk);
        }

        public object ActualObject(bool recursiveResolve = false)
        {
            // Go backwards
            var elements = Path.Split('/');
            object curNode = this.Parent;

            if (Absolute)
            {
                if (this.TreeNode != null)
                {
                    var firstElement = elements[0];
                    elements = elements.Skip(1).ToArray();
                    curNode = TreeNode.TreeView.Nodes[firstElement] as WZTreeNode;
                }
                else
                {
                    // Lets go all the way back...
                    while (true)
                    {
                        var lastNonNullNode = curNode;
                        if (curNode is WZTreeNode wztn)
                            curNode = wztn.Parent;
                        else if (curNode is PcomObject po)
                        {
                            curNode = po.Parent;
                            if (po.Parent == null)
                            {
                                curNode = po.TreeNode?.Parent as WZTreeNode;
                            }
                        }
                        else
                            throw new NotImplementedException("Not sure how to handle this: " + curNode);

                        if (curNode == null)
                        {
                            curNode = lastNonNullNode;
                            break;
                        }
                    }
                }
            }



            foreach (var element in elements)
            {
                if (element == ".") continue;
                if (element == "..")
                {
                    if (curNode is WZTreeNode wztn)
                        curNode = wztn.Parent;
                    else if (curNode is PcomObject po)
                    {
                        curNode = po.Parent;
                        if (po.Parent == null)
                        {
                            curNode = po.TreeNode?.Parent as WZTreeNode;
                        }
                    }
                    else
                        throw new NotImplementedException("Not sure how to handle this: " + curNode);
                }
                else
                {
                    bool retried = false;
                    var tmp = curNode;
                    // Saving this in a different far so i can change it
                    var elemName = element;
                    try_again:

                    if (curNode is WZTreeNode wztn)
                    {
                        var tn = wztn.Nodes[elemName] as WZTreeNode;
                        if (tn == null)
                        {
                            curNode = null;
                        }
                        else
                        {
                            tn.TryLoad(false);
                            curNode = tn?.WzObject;
                        }
                        
                    }
                    else if (curNode is PcomObject po)
                        curNode = po.Get(elemName);
                    else
                        throw new NotImplementedException("Not sure how to handle this: " + curNode);

                    if (curNode == null)
                    {
                        if (retried) return null;

                        if (tmp is WZTreeNode)
                        {
                            retried = true;
                            curNode = tmp;

                            elemName = element + ".img";
                            goto try_again;
                        }
                        // Don't know what to try next.
                        return null;
                    }
                }
            }

            if (recursiveResolve && curNode is WzUOL secondUol)
                curNode = secondUol.ActualObject() as PcomObject;

            return curNode;
        }
    }
}
