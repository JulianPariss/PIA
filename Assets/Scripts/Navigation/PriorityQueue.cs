using System;
using System.Collections.Generic;

namespace PIA1.Navigation
{
    public class PriorityQueue<T>
    {
        private struct HeapItem
        {
            public T Item;
            public float Priority;

            public HeapItem(T item, float priority)
            {
                Item = item;
                Priority = priority;
            }
        }

        private readonly List<HeapItem> _heap;

        public int Count => _heap.Count;
        public bool IsEmpty => _heap.Count == 0;

        public PriorityQueue(int initialCapacity = 32)
        {
            _heap = new List<HeapItem>(initialCapacity);
        }

        public void Enqueue(T item, float priority)
        {
            _heap.Add(new HeapItem(item, priority));
            HeapifyUp(_heap.Count - 1);
        }

        public T Dequeue()
        {
            if (_heap.Count == 0)
            {
                throw new InvalidOperationException("La cola de prioridad está vacía.");
            }

            T rootItem = _heap[0].Item;
            int lastIndex = _heap.Count - 1;

            _heap[0] = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);

            if (_heap.Count > 1)
            {
                HeapifyDown(0);
            }

            return rootItem;
        }

        public T Peek()
        {
            if (_heap.Count == 0)
            {
                throw new InvalidOperationException("La cola de prioridad está vacía.");
            }
            return _heap[0].Item;
        }

        public void Clear()
        {
            _heap.Clear();
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;

                if (_heap[index].Priority < _heap[parentIndex].Priority)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else
                {
                    break;
                }
            }
        }

        private void HeapifyDown(int index)
        {
            int count = _heap.Count;

            while (true)
            {
                int leftChild = index * 2 + 1;
                int rightChild = index * 2 + 2;
                int smallest = index;

                if (leftChild < count && _heap[leftChild].Priority < _heap[smallest].Priority)
                {
                    smallest = leftChild;
                }

                if (rightChild < count && _heap[rightChild].Priority < _heap[smallest].Priority)
                {
                    smallest = rightChild;
                }

                if (smallest != index)
                {
                    Swap(index, smallest);
                    index = smallest;
                }
                else
                {
                    break;
                }
            }
        }

        private void Swap(int a, int b)
        {
            HeapItem temp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = temp;
        }
    }
}
