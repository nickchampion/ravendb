﻿using System;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Client.Indexing;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Voron;

namespace Raven.Server.Documents.Indexes.Auto
{
    public class AutoMapIndexDefinition : IndexDefinitionBase
    {
        public AutoMapIndexDefinition(string collection, IndexField[] fields)
            : base(IndexNameFinder.FindMapIndexName(new[] { collection }, fields), new[] { collection }, IndexLockMode.Unlock, fields)
        {
            if (string.IsNullOrEmpty(collection))
                throw new ArgumentNullException(nameof(collection));

            if (fields.Length == 0)
                throw new ArgumentException("You must specify at least one field.", nameof(fields));
        }

        protected override void PersisFields(TransactionOperationContext context, BlittableJsonTextWriter writer)
        {
            PersistMapFields(context, writer);
        }

        protected override void FillIndexDefinition(IndexDefinition indexDefinition)
        {
            var map = $"{Collections.First()}:[{string.Join(";", MapFields.Select(x => $"<Name:{x.Value.Name},Sort:{x.Value.SortOption},Highlight:{x.Value.Highlighted}>"))}]";
            indexDefinition.Maps.Add(map);
        }

        public override bool Equals(IndexDefinitionBase other, bool ignoreFormatting, bool ignoreMaxIndexOutputs)
        {
            var otherDefinition = other as AutoMapIndexDefinition;
            if (otherDefinition == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) == false)
                return false;

            if (Collections.SequenceEqual(otherDefinition.Collections) == false)
                return false;

            if (MapFields.SequenceEqual(otherDefinition.MapFields) == false)
                return false;

            return true;
        }

        public static AutoMapIndexDefinition Load(StorageEnvironment environment)
        {
            using (var pool = new UnmanagedBuffersPool(nameof(AutoMapIndexDefinition)))
            using (var context = new JsonOperationContext(pool))
            using (var tx = environment.ReadTransaction())
            {
                var tree = tx.CreateTree("Definition");
                var result = tree.Read(DefinitionSlice);
                if (result == null)
                    return null;

                using (var reader = context.ReadForDisk(result.Reader.AsStream(), string.Empty))
                {
                    int lockModeAsInt;
                    reader.TryGet(nameof(LockMode), out lockModeAsInt);

                    BlittableJsonReaderArray jsonArray;
                    reader.TryGet(nameof(Collections), out jsonArray);

                    var collection = jsonArray.GetStringByIndex(0);

                    reader.TryGet(nameof(MapFields), out jsonArray);

                    var fields = new IndexField[jsonArray.Length];
                    for (var i = 0; i < jsonArray.Length; i++)
                    {
                        var json = jsonArray.GetByIndex<BlittableJsonReaderObject>(i);

                        string name;
                        json.TryGet(nameof(IndexField.Name), out name);

                        bool highlighted;
                        json.TryGet(nameof(IndexField.Highlighted), out highlighted);

                        int sortOptionAsInt;
                        json.TryGet(nameof(IndexField.SortOption), out sortOptionAsInt);

                        var field = new IndexField
                        {
                            Name = name,
                            Highlighted = highlighted,
                            Storage = FieldStorage.No,
                            SortOption = (SortOptions?)sortOptionAsInt,
                            Indexing = FieldIndexing.Default
                        };

                        fields[i] = field;
                    }

                    return new AutoMapIndexDefinition(collection, fields)
                    {
                        LockMode = (IndexLockMode)lockModeAsInt
                    };
                }
            }
        }
    }
}