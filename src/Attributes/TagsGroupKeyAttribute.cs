﻿using System;

namespace Geta.Tags.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class TagsGroupKeyAttribute : Attribute
    {
        public TagsGroupKeyAttribute(string key)
        {
            this.Key = key;
        }

        public virtual string Key { get; private set; }
    }
}