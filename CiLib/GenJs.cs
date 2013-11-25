// GenJs.cs - JavaScript code generator
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
using System.Collections.Generic;
using System.Text;

namespace Foxoft.Ci {

  public class GenJs : CiGenerator {
    CiClass CurrentClass;

    public GenJs(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenJs() : base() {
      Namespace = "cito";
      TranslateSymbolName = JS_SymbolNameTranslator;
      Decode_ARRAYBEGIN = "[ ";
      Decode_ARRAYEND = " ]";

    }

    public string JS_SymbolNameTranslator(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      if (aSymbol is CiEnumValue) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiField) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiConst) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiMethod) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiDelegate) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiBinaryResource) {
        StringBuilder res = new StringBuilder();
        res.Append("CI_BINARY_RESOURCE_");
        CiBinaryResource resource = (CiBinaryResource)aSymbol;
        foreach (char c in resource.Name) {
          res.Append(CiLexer.IsLetter(c) ? char.ToUpperInvariant(c) : '_');
        }
        name = res.ToString();
      }
      else {
        name = SymbolNameTranslator(aSymbol);
      }
      return name;
    }

    protected override void WriteDocCode(CiCodeDoc doc) {
      if (doc == null)
        return;
      // TODO
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        Write("new Array(");
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        Write(')');
      }
    }

    public override bool WriteInit(CiType type) {
      if (type is CiClassStorageType || type is CiArrayStorageType) {
        Write(" = ");
        WriteNew(type);
        return true;
      }
      return false;
    }

    void Write(CiField field) {
      WriteDocCode(field.Documentation);
      WriteFormat("this.{0}", DecodeSymbol(field));
      CiType type = field.Type;
      if (type == CiBoolType.Value) {
        Write(" = false");
      }
      else if (type == CiByteType.Value || type == CiIntType.Value) {
        Write(" = 0");
      }
      else if (type is CiEnum) {
        WriteFormat(" = {0}", DecodeValue(null, ((CiEnum)type).Values[0]));
      }
      else if (!WriteInit(type)) {
        Write(" = null");
      }
      WriteLine(";");
    }

    public override string DecodeSymbol(CiSymbol symbol) {
      SymbolMapping mappedSymbol = FindSymbol(symbol);
      string prefix = "";
      if (symbol is CiConst) {
        prefix = DecodeSymbol(CurrentClass) + '.';
      }
      return (mappedSymbol != null) ? prefix + mappedSymbol.NewName : prefix + TranslateSymbolName(symbol);
    }

    public override CiPriority GetPriority(CiExpr expr) {
      if (expr is CiPropertyAccess) {
        CiProperty prop = ((CiPropertyAccess)expr).Property;
        if (prop == CiLibrary.SByteProperty) {
          return CiPriority.Additive;
        }
        if (prop == CiLibrary.LowByteProperty) {
          return CiPriority.And;
        }
      }
      else if (expr is CiBinaryExpr) {
        if (((CiBinaryExpr)expr).Op == CiToken.Slash) {
          return CiPriority.Postfix;
        }
      }
      return base.GetPriority(expr);
    }

    protected virtual void WriteInitArrayStorageVar(CiVar stmt) {
      WriteLine(";");
      Write("Ci.clearArray(");
      Write(stmt.Name);
      Write(", ");
      Translate(stmt.InitialValue);
      Write(")");
      UseFunction("ClearArrayMethod");
    }

    void WriteBuiltins() {
      List<string[]> code = new List<string[]>();
      if (IsUsedFunction("SubstringMethod")) {
        code.Add(new string[] {
          "substring : function(s, offset, length)",
          "return s.substring(offset, offset + length);"
        });
      }
      if (IsUsedFunction("CopyArrayMethod")) {
        code.Add(new string[] {
          "copyArray : function(sa, soffset, da, doffset, length)",
          "for (var i = 0; i < length; i++)",
          "\tda[doffset + i] = sa[soffset + i];"
        });
      }
      if (IsUsedFunction("BytesToStringMethod")) {
        code.Add(new string[] {
          "bytesToString : function(a, offset, length)",
          "var s = \"\";",
          "for (var i = 0; i < length; i++)",
          "\ts += String.fromCharCode(a[offset + i]);",
          "return s;"
        });
      }
      if (IsUsedFunction("ClearArrayMethod")) {
        code.Add(new string[] {
          "clearArray : function(a, value)",
          "for (var i = 0; i < a.length; i++)",
          "\ta[i] = value;"
        });
      }
      if (code.Count > 0) {
        WriteLine("var Ci = {");
        OpenBlock(false);
        for (int i = 0; ;) {
          string[] lines = code[i];
          Write(lines[0]);
          WriteLine(" {");
          OpenBlock(false);
          for (int j = 1; j < lines.Length; j++) {
            WriteLine(lines[j]);
          }
          CloseBlock(false);
          Write('}');
          if (++i >= code.Count) {
            break;
          }
          WriteLine(",");
        }
        WriteLine();
        CloseBlock(false);
        WriteLine("};");
      }
    }

    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
        foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          ((CiClass)symbol).WriteStatus = CiWriteStatus.NotYet;
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Translate(symbol);
        }
        else if (symbol is CiClass) {
          Translate(symbol);
        }
      }
      WriteBuiltins();
      CloseFile();
    }

    public override void InitOperators() {
      base.InitOperators();
      BinaryOperators.Declare(CiToken.Slash, CiPriority.Multiplicative, false, ConvertOperatorSlash, " / ");
    }

    public void ConvertOperatorSlash(CiBinaryExpr expr, BinaryOperatorInfo token) {
      Write("Math.floor(");
      WriteChild(CiPriority.Multiplicative, expr.Left);
      Write(token.Symbol);
      WriteChild(CiPriority.Multiplicative, expr.Right, true);
      Write(')');
    }

    #region Converter Symbols
    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteDocCode(enu.Documentation);
      WriteFormat("var {0} = ", DecodeSymbol(enu));
      OpenBlock();
      for (int i = 0; i < enu.Values.Length; i++) {
        if (i > 0) {
          WriteLine(",");
        }
        CiEnumValue value = enu.Values[i];
        WriteDocCode(value.Documentation);
        WriteFormat("{0} : {1}", DecodeSymbol(value), i);
      }
      WriteLine();
      CloseBlock();
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      if (method.CallType == CiCallType.Abstract) {
        return;
      }
      WriteLine();
      WriteFormat("{0}.", DecodeSymbol(method.Class));
      if (method.CallType != CiCallType.Static) {
        Write("prototype.");
      }
      WriteFormat("{0} = function(", DecodeSymbol(method));
      bool first = true;
      foreach (CiParam param in method.Signature.Params) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        Write(DecodeSymbol(param));
      }
      Write(") ");
      Translate(method.Body);
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      WriteLine("{0} = {1};", DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      // topological sorting of class hierarchy
      if (klass.WriteStatus == CiWriteStatus.Done) {
        return;
      }
      if (klass.WriteStatus == CiWriteStatus.InProgress) {
        throw new ResolveException("Circular dependency for class {0}", klass.Name);
      }
      klass.WriteStatus = CiWriteStatus.InProgress;
      if (klass.BaseClass != null) {
        Translate(klass.BaseClass);
      }
      klass.WriteStatus = CiWriteStatus.Done;
      this.CurrentClass = klass;
      WriteLine();
      WriteDocCode(klass.Documentation);
      WriteFormat("function {0}() ", DecodeSymbol(klass));
      OpenBlock();
      foreach (CiSymbol member in klass.Members) {
        if (member is CiField) {
          Write((CiField)member);
        }
      }
      if (klass.Constructor != null) {
        WriteCode(klass.Constructor.Body.Statements);
      }
      CloseBlock();
      if (klass.BaseClass != null) {
        WriteLine("{0}.prototype = new {1}();", DecodeSymbol(klass), DecodeSymbol(klass.BaseClass));
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          Translate(member);
        }
        else if (member is CiConst && member.Visibility == CiVisibility.Public) {
          Symbol_CiConst(member);
        }
      }
      foreach (CiConst konst in klass.ConstArrays) {
        Symbol_CiConst((CiConst)konst);
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("{0}.{1} = {2};", DecodeSymbol(CurrentClass), DecodeSymbol(resource), DecodeValue(resource.Type, resource.Content));
      }
      this.CurrentClass = null;
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      WriteFormat(".{0}", DecodeSymbol(expr.Field));
    }

    public override void Expression_CiBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      WriteFormat("{0}.{1}", DecodeSymbol(CurrentClass), DecodeSymbol(expr.Resource));
    }
    #endregion

    #region Converter Statements
    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      Write("var ");
      Write(stmt.Name);
      WriteInit(stmt.Type);
      if (stmt.InitialValue != null) {
        if (stmt.Type is CiArrayStorageType)
          WriteInitArrayStorageVar(stmt);
        else {
          Write(" = ");
          Translate(stmt.InitialValue);
        }
      }
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw ");
      Translate(stmt.Message);
      WriteLine(";");
    }

    public override void Statement_CiDelete(ICiStatement statement) {
    }
    #endregion

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write('(');
      WriteChild(CiPriority.Xor, expr.Obj);
      Write(" ^ 128) - 128");
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(" & 0xff");
    }

    public override void Library_Length(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".length");
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      Write("Math.floor(");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public override void Library_CharAt(CiMethodCall expr) {
      Translate(expr.Obj);
      Write(".charCodeAt(");
      Translate(expr.Arguments[0]);
      Write(')');
    }

    public override void Library_Substring(CiMethodCall expr) {
      if (expr.Arguments[0].HasSideEffect) {
        Write("Ci.substring(");
        Translate(expr.Obj);
        Write(", ");
        Translate(expr.Arguments[0]);
        Write(", ");
        Translate(expr.Arguments[1]);
        Write(')');
        UseFunction("SubstringMethod");
      }
      else {
        Translate(expr.Obj);
        Write(".substring(");
        Translate(expr.Arguments[0]);
        Write(", ");
        Translate(new CiBinaryExpr { Left = expr.Arguments[0], Op = CiToken.Plus, Right = expr.Arguments[1] });
        Write(')');
      }
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      Write("Ci.copyArray(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(", ");
      Translate(expr.Arguments[2]);
      Write(", ");
      Translate(expr.Arguments[3]);
      Write(')');
      UseFunction("CopyArrayMethod");
    }

    public override void Library_ToString(CiMethodCall expr) {
      Write("Ci.bytesToString(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
      UseFunction("BytesToStringMethod");
    }

    public override void Library_Clear(CiMethodCall expr) {
      Write("Ci.clearArray(");
      Translate(expr.Obj);
      Write(", 0)");
      UseFunction("ClearArrayMethod");
    }
    #endregion

  }
}
