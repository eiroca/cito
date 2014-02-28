// GenC89.cs - C89 code generator
//
// Copyright (C) 2011-2013  Piotr Fusik
// Copyright (C) 2013  Enrico Croce
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

namespace Foxoft.Ci {

  public class GenC89 : GenC {
    public GenC89(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenC89() : base() {
      Decode_TRUEVALUE = "TRUE";
      Decode_FALSEVALUE = "FALSE";
    }

    public override TypeInfo Type_CiBoolType(CiType type) {
      return new TypeInfo(type, "cibool", Decode_FALSEVALUE);
    }

    protected override void WriteBoolType() {
      WriteLine("typedef int cibool;");
      WriteLine("#ifndef TRUE");
      WriteLine("#define TRUE 1");
      WriteLine("#endif");
      WriteLine("#ifndef FALSE");
      WriteLine("#define FALSE 0");
      WriteLine("#endif");
    }

    void WriteVar(CiVar def) {
      Write(ToString(def.Type, def));
      WriteLine(";");
      def.WriteInitialValue = true;
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      Write("(int) ((double) ");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public override void Statement_CiFor(ICiStatement statement) {
      CiFor stmt = (CiFor)statement;
      CiVar def = stmt.Init as CiVar;
      if (def != null) {
        OpenBlock();
        WriteVar(def);
        base.Statement_CiFor(stmt);
        CloseBlock();
      }
      else {
        base.Statement_CiFor(stmt);
      }
    }

    public override void Statement_CiConst(ICiStatement statement) {
    }

    protected override void StartBlock(ICiStatement[] statements) {
      // variable and const definitions, with initializers if possible
      bool canInitVar = true;
      foreach (ICiStatement stmt in statements) {
        if (stmt is CiConst) {
          Symbol_CiConst((CiConst)stmt);
          continue;
        }
        CiVar def = stmt as CiVar;
        if (canInitVar) {
          if (def != null && IsInlineVar(def)) {
            base.Statement_CiVar(stmt);
            def.WriteInitialValue = false;
            WriteLine(";");
            continue;
          }
          canInitVar = false;
        }
        if (def != null) {
          WriteVar(def);
        }
      }
    }

    public override void Statement_CiBlock(ICiStatement statement) {
      CiBlock block = (CiBlock)statement;
      OpenBlock();
      StartBlock(block.Statements);
      WriteCode(block.Statements);
      CloseBlock();
    }

    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      if (stmt.WriteInitialValue) {
        if (stmt.InitialValue != null) {
          if (stmt.Type is CiArrayStorageType) {
            WriteClearArray(new CiVarAccess { Var = stmt });
          }
          else {
            Translate(new CiAssign { Target = new CiVarAccess { Var = stmt }, Op = CiToken.Assign, Source = stmt.InitialValue });
          }
        }
        else if (stmt.Type is CiClassStorageType) {
          CiClass klass = ((CiClassStorageType)stmt.Type).Class;
          if (klass.Constructs) {
            WriteConstruct(klass, stmt);
          }
        }
        stmt.WriteInitialValue = false;
      }
    }

    void WriteSwitchDefs(ICiStatement[] body) {
      foreach (ICiStatement stmt in body) {
        if (stmt is CiConst) {
          Symbol_CiConst((CiConst)stmt);
        }
        else if (stmt is CiVar) {
          WriteVar((CiVar)stmt);
        }
      }
    }

    protected override void StartSwitch(CiSwitch stmt) {
      OpenBlock(false);
      foreach (CiCase kase in stmt.Cases) {
        WriteSwitchDefs(kase.Body);
      }
      if (stmt.DefaultBody != null) {
        WriteSwitchDefs(stmt.DefaultBody);
      }
      CloseBlock(false);
    }

    protected override void StartCase(ICiStatement stmt) {
    }
  }
}
