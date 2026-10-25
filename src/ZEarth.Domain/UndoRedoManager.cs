using System;
using System.Collections.Generic;

namespace ZEarth.Domain
{
    public interface IUndoableAction
    {
        string Description { get; }
        void Execute();
        void Unexecute();
    }

    public sealed class UndoRedoManager
    {
        private readonly Stack<IUndoableAction> _undoStack = new();
        private readonly Stack<IUndoableAction> _redoStack = new();
        private const int MaxUndoDepth = 100;

        public event EventHandler? StateChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public void ExecuteAction(IUndoableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            action.Execute();

            _undoStack.Push(action);
            _redoStack.Clear(); // Branch invalidation for redo history

            if (_undoStack.Count > MaxUndoDepth)
            {
                // Prune oldest
                var items = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = MaxUndoDepth - 1; i >= 0; i--)
                {
                    _undoStack.Push(items[i]);
                }
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool Undo()
        {
            if (!CanUndo) return false;

            var action = _undoStack.Pop();
            action.Unexecute();
            _redoStack.Push(action);

            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;

            var action = _redoStack.Pop();
            action.Execute();
            _undoStack.Push(action);

            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Concrete Actions for Tree CRUD
    public sealed class AddPlaceAction : IUndoableAction
    {
        private readonly IList<PlaceItem> _collection;
        private readonly PlaceItem _item;

        public string Description => $"Add '{_item.Name}'";

        public AddPlaceAction(IList<PlaceItem> collection, PlaceItem item)
        {
            _collection = collection;
            _item = item;
        }

        public void Execute()
        {
            if (!_collection.Contains(_item))
            {
                _collection.Add(_item);
            }
        }

        public void Unexecute()
        {
            _collection.Remove(_item);
        }
    }

    public sealed class DeletePlaceAction : IUndoableAction
    {
        private readonly IList<PlaceItem> _parentCollection;
        private readonly PlaceItem _item;
        private int _originalIndex;

        public string Description => $"Delete '{_item.Name}'";

        public DeletePlaceAction(IList<PlaceItem> parentCollection, PlaceItem item)
        {
            _parentCollection = parentCollection;
            _item = item;
            _originalIndex = parentCollection.IndexOf(item);
        }

        public void Execute()
        {
            _originalIndex = _parentCollection.IndexOf(_item);
            if (_originalIndex >= 0)
            {
                _parentCollection.RemoveAt(_originalIndex);
            }
        }

        public void Unexecute()
        {
            if (_originalIndex >= 0 && _originalIndex <= _parentCollection.Count)
            {
                _parentCollection.Insert(_originalIndex, _item);
            }
            else
            {
                _parentCollection.Add(_item);
            }
        }
    }

    public sealed class MoveToMyPlacesAction : IUndoableAction
    {
        private readonly IList<PlaceItem> _sourceCollection;
        private readonly IList<PlaceItem> _targetCollection;
        private readonly PlaceItem _item;
        private int _originalIndex;

        public string Description => $"Save '{_item.Name}' to My Places";

        public MoveToMyPlacesAction(IList<PlaceItem> sourceCollection, IList<PlaceItem> targetCollection, PlaceItem item)
        {
            _sourceCollection = sourceCollection;
            _targetCollection = targetCollection;
            _item = item;
        }

        public void Execute()
        {
            _originalIndex = _sourceCollection.IndexOf(_item);
            if (_originalIndex >= 0) _sourceCollection.RemoveAt(_originalIndex);
            _targetCollection.Add(_item);
        }

        public void Unexecute()
        {
            _targetCollection.Remove(_item);
            if (_originalIndex >= 0 && _originalIndex <= _sourceCollection.Count)
            {
                _sourceCollection.Insert(_originalIndex, _item);
            }
            else
            {
                _sourceCollection.Add(_item);
            }
        }
    }
}
