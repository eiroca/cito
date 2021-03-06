// SymbolTable.cs - symbol table
//
// Copyright (C) 2011  Piotr Fusik
// Copyright (C) 2013-2019  Enrico Croce
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Foxoft.Ci {

  public class SymbolTable : IEnumerable<CiSymbol> {

    public SymbolTable Parent;
    readonly SortedDictionary<string, CiSymbol> Dict = new SortedDictionary<string, CiSymbol>(StringComparer.Ordinal);

    public SymbolTable(SymbolTable Parent) {
      this.Parent = Parent;
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.Dict.Values.GetEnumerator();
    }

    public IEnumerator<CiSymbol> GetEnumerator() {
      return this.Dict.Values.GetEnumerator();
    }

    public void Add(CiSymbol symbol) {
      string name = symbol.Name;
      for (SymbolTable t = this; t != null; t = t.Parent) {
        if (t.Dict.ContainsKey(name)) {
          throw new ParseException(symbol.Position, "Symbol {0} already defined", name);
        }
      }
      this.Dict.Add(name, symbol);
    }

    public CiSymbol TryLookup(string name) {
      for (SymbolTable t = this; t != null; t = t.Parent) {
        CiSymbol result;
        if (t.Dict.TryGetValue(name, out result)) {
          return result;
        }
      }
      return null;
    }

    void Dump() {
      foreach (CiSymbol symbol in this) {
        Console.Error.Write("{0} {1}, ", symbol.GetType().Name, symbol.Name);
      }
      Console.Error.WriteLine();
      if (Parent != null) {
        Parent.Dump();
      }
    }

    public CiSymbol Lookup(CiSymbol symbol) {
      CiSymbol result = TryLookup(symbol.Name);
      if (result == null) {
        throw new ResolveException(symbol, "Unknown symbol {0}");
      }
      return result;
    }

    public CiSymbol Lookup(CodePosition position, string name) {
      CiSymbol result = TryLookup(name);
      if (result == null) {
        throw new ResolveException(position, "Unknown symbol {0}", name);
      }
      return result;
    }
  }

}
