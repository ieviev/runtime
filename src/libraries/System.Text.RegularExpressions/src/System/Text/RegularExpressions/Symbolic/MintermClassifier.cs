﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Classifies characters as their corresponding minterm IDs.</summary>
    /// <remarks>
    /// Minterms are a mechanism for compressing the input character space, or "alphabet",
    /// by creating an equivalence class for all characters treated the same.  For example,
    /// in the expression "[0-9]*", the 10 digits 0 through 9 are all treated the same as each
    /// other, and every other of the 65,526 characters are treated the same as each other,
    /// so there are two minterms, one for the digits, and one for everything else. Minterms
    /// are computed in such a way that every character maps to one and only one minterm.
    /// While in the limit there could be one minterm per character, in practice the number
    /// of minterms for any reasonable expression is way less, and in fact is typically
    /// less than 64.
    /// </remarks>
    internal sealed class MintermClassifier
    {
        private static readonly int[] s_emptyLookup = new int[ushort.MaxValue + 1];
        /// <summary>An array used to map characters to minterms</summary>
        private readonly int[] _lookup;
        private readonly bool _isAsciiOnly;

        /// <summary>Create a classifier that maps a character to the ID of its associated minterm.</summary>
        /// <param name="minterms">A BDD for classifying all characters (ASCII and non-ASCII) to their corresponding minterm IDs.</param>
        public MintermClassifier(BDD[] minterms)
        {
            Debug.Assert(minterms.Length > 0, "Requires at least");


            if (minterms.Length == 1)
            {
                // With only a single minterm, the mapping is trivial: everything maps to it (ID 0).
                _lookup = s_emptyLookup;
                return;
            }

            // low memory variant could create an ascii-only array
            // cheaper to iterate twice than allocate an array and potentially not use it
            _isAsciiOnly = true;
            for (int mintermId = 1; mintermId < minterms.Length; mintermId++)
            {
                (uint, uint)[] mintermRanges = BDDRangeConverter.ToRanges(minterms[mintermId]);
                if (mintermRanges[^1].Item2 >= 128)
                {
                    _isAsciiOnly = false;
                }
            }


            // assign minterm category for every char
            // unused characters in minterm 0 get mapped to zero
            int[] lookup = new int[_isAsciiOnly ? 128 : 65536];
            for (int mintermId = 1; mintermId < minterms.Length; mintermId++)
            {
                // precompute all assigned minterm categories
                (uint, uint)[] mintermRanges = BDDRangeConverter.ToRanges(minterms[mintermId]);
                foreach ((uint start, uint end) in mintermRanges)
                {
                    // assign character ranges in bulk
                    Span<int> slice = lookup.AsSpan((int)start, (int)(end + 1 - start));
                    slice.Fill(mintermId);
                }
            }
            _lookup = lookup;
        }

        /// <summary>Gets the ID of the minterm associated with the specified character.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMintermID(int c)
        {
            if (_isAsciiOnly && (c >= 128))
            {
                return 0;
            }
            // high performance variant would use a span directly
            // but this is not possible in low memory constraints
            return _lookup[c];
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public Span<int> LookupSpan() => _lookup.AsSpan();
    }
}
