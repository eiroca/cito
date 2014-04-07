// GenD.cs - D code generator
//
// Copyright (C) 2011-2014  Adrian Matoga
// Copyright (C) 2013-2014  Enrico Croce
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
using System.Text;

namespace Foxoft.Ci {

  public class GenD : CiGenerator {
    protected CiClass CurrentClass;

    public GenD(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenD() : base() {
      Namespace = "D";
      CommentContinueStr = "///";
      CommentBeginStr = "";
      CommentEndStr = "";
      CommentCodeBegin = "<code>";
      CommentCodeEnd = "</code>";
    }

    public override string[] GetReservedWords() {
      String[] result = new String[] { "delete", "break", "continue", "do", "while", "for", "if", "native", "return", "switch", "case", "default", "throw", "module" };
      return result;
    }

    protected override void WriteBanner() {
      base.WriteBanner();
      WriteLine("import std.utf;");
      WriteLine();
    }

    public override string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is Array) {
        res.Append("[ ");
        res.Append(DecodeArray(type, (Array)value));
        res.Append(" ]");
      }
      else {
        res.Append(base.DecodeValue(type, value));
      }
      return res.ToString();
    }

    public override void Expression_CiConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      CiConst konst = (CiConst)expr.Const;
      if ((konst.Class != null) && (konst.Class != CurrentClass)) {
        WriteFormat("{0}.{1}", DecodeSymbol(konst.Class), DecodeSymbol(konst));
      }
      else {
        Write(DecodeSymbol(konst));
      }
    }

    public override string DecodeVisibility(CiVisibility visibility) {
      string res = "";
      switch (visibility) {
        case CiVisibility.Dead:
          // TODO: if it isn't called anywhere in known sources, maybe
          // it should be marked as "export"?
        case CiVisibility.Private:
          res = "private";
          break;
        case CiVisibility.Internal:
          // TODO: maybe we should use "package"
          break;
        case CiVisibility.Public:
          break;
      }
      return res;
    }

    public override string DecodeType(CiType type) {
      StringBuilder sb = new StringBuilder();
      bool haveConst = false;
      int strt = 0;
      while (type is CiArrayType) {
        sb.Insert(0, "[]");
        if (!haveConst) {
          CiArrayPtrType ptr = type as CiArrayPtrType;
          if (ptr != null && ptr.Writability != PtrWritability.ReadWrite) {
            sb.Insert(0, ")");
            haveConst = true;
          }
        }
        type = ((CiArrayType)type).ElementType;
      }
      if (haveConst) {
        sb.Insert(0, "const(");
        strt = 6;
      }
      sb.Insert(strt, base.DecodeType(type.BaseType));
      return sb.ToString();
    }

    void WriteDocString(string text, bool inMacro) {
      foreach (char c in text) {
        switch (c) {
          case '&':
            Write("&amp;");
            break;
          case '<':
            Write("&lt;");
            break;
          case '>':
            Write("&gt;");
            break;
          case '\n':
            WriteLine();
            Write("/// ");
            break;
          default:
            if (inMacro) {
              switch (c) {
                case '$':
                  Write("&#36;");
                  break;
                case '(':
                  Write("$(LPAREN)");
                  break;
                case ')':
                  Write("$(RPAREN)");
                  break;
                default:
                  Write(c);
                  break;
              }
            }
            else Write(c);
            break;
        }
      }
    }

    protected override void WriteDocPara(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDocString(text.Text, false);
          continue;
        }
        // TODO: $(D_CODE x) pastes "<pre>x</pre>" -
        // find some better alternative
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          WriteDocString(code.Text, true);
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    protected override void WriteDocBlock(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        WriteLine("/// $(UL");
        foreach (CiDocPara item in list.Items) {
          Write("/// $(LI ");
          WriteDocPara(item);
          WriteLine(")");
        }
        Write("/// )");
        WriteLine();
        Write("/// ");
        return;
      }
      WriteDocPara((CiDocPara)block);
    }

    protected override void WriteDocCode(CiCodeDoc doc) {
      if (doc == null) {
        return;
      }
      Write("/// ");
      WriteDocPara(doc.Summary);
      WriteLine();
      if (doc.Details.Length > 0) {
        WriteLine("///");
        Write("/// ");
        foreach (CiDocBlock block in doc.Details) {
          WriteDocBlock(block);
        }
        WriteLine();
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

    public override CiPriority GetPriority(CiExpr expr) {
      // TODO: check if this compatible with D priorities
      if (expr is CiPropertyAccess) {
        CiProperty prop = ((CiPropertyAccess)expr).Property;
        if (prop == CiLibrary.SByteProperty || prop == CiLibrary.LowByteProperty) {
          return CiPriority.Prefix;
        }
      }
      else if (expr is CiCoercion) {
        CiCoercion c = (CiCoercion)expr;
        if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value) {
          return CiPriority.Prefix;
        }
      }
      return base.GetPriority(expr);
    }

    public override void WriteNew(CiType type) {
      WriteFormat("new {0}", base.DecodeType(type.BaseType));
      CiArrayStorageType arrayType = type as CiArrayStorageType;
      if (arrayType != null) {
        WriteInitializer(arrayType);
      }
    }

    protected override void WriteFallthrough(CiExpr expr) {
      Write("goto ");
      if (expr != null) {
        Write("case ");
        Translate(expr);
      }
      else {
        Write("default");
      }
      WriteLine(";");
    }

    protected override void EndSwitch(CiSwitch stmt) {
      if (stmt.DefaultBody == null) {
        WriteLine("default:");
        OpenBlock(false);
        WriteLine("break;");
        CloseBlock();
      }
    }

    void WriteSignature(CiDelegate del) {
      WriteFormat("{0} {1}", DecodeType(del.ReturnType), DecodeSymbol(del));
      WriteArgumentList(del);
    }

    void WriteArgumentList(CiDelegate del) {
      Write("(");
      bool first = true;
      foreach (CiParam param in del.Params) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        WriteFormat("{0} {1}", DecodeType(param.Type), DecodeSymbol(param));
      }
      Write(')');
    }

    #region Converter Types
    public override TypeInfo Type_CiByteType(CiType type) {
      return new TypeInfo(type, "ubyte", "0");
    }

    public override TypeInfo Type_CiClassStorageType(CiType type) {
      TypeInfo result = new TypeInfo(type, type.Name, "null");
      return result;
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        Write("cast(ubyte) ");
        WriteChild(expr, (CiExpr)expr.Inner); // TODO: Assign
      }
      else {
        base.Expression_CiCoercion(expression);
      }
    }
    #endregion

    #region Converter Statements
    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      WriteFormat("{0} {1}", DecodeType(stmt.Type), DecodeSymbol(stmt));
      if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
        Write(" = ");
        Translate(stmt.InitialValue);
      }
    }

    public override void Statement_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      if (assign.Op == CiToken.AddAssign && assign.Target.Type is CiStringStorageType) {
        Translate(assign.Target);
        Write(" ~= ");
        WriteInline(assign.Source);
      }
      else {
        base.Statement_CiAssign(statement);
      }
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw new Exception(");
      Translate(stmt.Message);
      WriteLine(");");
    }

    public override void Statement_CiDelete(ICiStatement statement) {
    }
    #endregion

    #region Converter Symbols
    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      WriteDocCode(field.Documentation);
      WriteLine("{0} {1} {2};", DecodeVisibility(field.Visibility), DecodeType(field.Type), DecodeSymbol(field));
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      if (konst.Visibility != CiVisibility.Public) {
        return;
      }
      WriteDocCode(konst.Documentation);
      WriteLine("public static immutable({0}) {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      WriteDocCode(method.Documentation);
      bool paramsStarted = false;
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          if (!paramsStarted) {
            WriteLine("/// Params:");
            paramsStarted = true;
          }
          WriteFormat("/// {0} = ", DecodeSymbol(param));
          WriteDocPara(param.Documentation.Summary);
          WriteLine();
        }
      }
      Write(DecodeVisibility(method.Visibility));
      switch (method.CallType) {
        case CiCallType.Static:
          Write("static ");
          break;
        case CiCallType.Normal:
          if (method.Visibility != CiVisibility.Private) Write("final ");
          break;
        case CiCallType.Abstract:
          Write("abstract ");
          break;
        case CiCallType.Virtual:
          break;
        case CiCallType.Override:
          Write("override ");
          break;
      }
      WriteSignature(method.Signature);
      if (method.CallType == CiCallType.Abstract) {
        WriteLine(";");
      }
      else {
        WriteLine();
        Translate(method.Body);
      }
    }

    public override void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      WriteLine();
      WriteDocCode(klass.Documentation);
      Write(DecodeVisibility(klass.Visibility));
      OpenClass(klass.IsAbstract, klass, " : ");
      CurrentClass = klass;
      bool hasConstructor = klass.Constructor != null;
      foreach (CiSymbol member in klass.Members) {
        if (!hasConstructor) {
          CiField field = member as CiField;
          if (field != null && (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)) {
            hasConstructor = true;
          }
        }
        Translate(member);
      }
      foreach (CiConst konst in klass.ConstArrays) {
        if (konst.Visibility != CiVisibility.Public) {
          string name = konst.Class == CurrentClass ? konst.Name : konst.GlobalName;
          WriteLine("static immutable({0}) {1} = {2};", DecodeType(konst.Type), name, DecodeValue(konst.Type, konst.Value));
        }
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        // FIXME: it's better to import(resources) from binary files,
        // rather than pasting tons of magic numbers in the source.
        WriteLine("static immutable(ubyte[]) {0} = {1};", DecodeSymbol(resource), DecodeValue(resource.Type, resource.Content));
      }
      if (hasConstructor) {
        WriteLine("this()");
        OpenBlock();
        foreach (CiSymbol member in klass.Members) {
          CiField field = member as CiField;
          if (field != null && (field.Type is CiClassStorageType || field.Type is CiArrayStorageType)) {
            Write(DecodeSymbol(field));
            WriteInit(field.Type);
            WriteLine(";");
          }
        } 
        if (klass.Constructor != null) {
          WriteCode(klass.Constructor.Body.Statements);
        }
        CloseBlock();
      }
      CloseBlock();
      CurrentClass = null;
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      WriteDocCode(del.Documentation);
      Write(DecodeVisibility(del.Visibility));
      WriteLine("alias {0} = {1} delegate", DecodeSymbol(del), DecodeType(del.ReturnType));
      WriteArgumentList(del);
      WriteLine(";");
    }

    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteDocCode(enu.Documentation);
      WriteLine("{0} enum {1}", DecodeVisibility(enu.Visibility), DecodeSymbol(enu));
      OpenBlock();
      bool first = true;
      foreach (CiEnumValue value in enu.Values) {
        if (first) {
          first = false;
        }
        else {
          WriteLine(",");
        }
        WriteDocCode(value.Documentation);
        Write(DecodeSymbol(value));
      }
      WriteLine();
      CloseBlock();
    }
    #endregion

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write("cast(byte) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      Write("cast(ubyte) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_Length(CiPropertyAccess expr) {
      Write("cast(int) ");
      WriteChild(expr, expr.Obj);
      Write(".length");
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      Write("cast(int) (cast(long) ");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public override void Library_CharAt(CiMethodCall expr) {
      Translate(expr.Obj);
      Write('[');
      Translate(expr.Arguments[0]);
      Write(']');
    }

    public override void Library_Substring(CiMethodCall expr) {
      Translate(expr.Obj);
      Write('[');
      Translate(expr.Arguments[0]);
      Write(" .. (");
      Translate(expr.Arguments[0]);
      Write(") + ");
      Translate(expr.Arguments[1]);
      Write(']');
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      Translate(expr.Arguments[1]);
      Write('[');
      Translate(expr.Arguments[2]);
      Write(" .. (");
      Translate(expr.Arguments[2]);
      Write(") + ");
      Translate(expr.Arguments[3]);
      Write("] = ");
      Translate(expr.Obj);
      Write('[');
      Translate(expr.Arguments[0]);
      Write(" .. (");
      Translate(expr.Arguments[0]);
      Write(") + ");
      Translate(expr.Arguments[3]);
      Write(']');
    }

    public override void Library_ToString(CiMethodCall expr) {
      Write("toUTF8(cast(char[]) ");
      Translate(expr.Obj);
      Write('[');
      Translate(expr.Arguments[0]);
      Write(" .. (");
      Translate(expr.Arguments[0]);
      Write(") + ");
      Translate(expr.Arguments[1]);
      Write("])");
    }

    public override void Library_Clear(CiMethodCall expr) {
      Translate(expr.Obj);
      Write("[] = 0");
    }
    #endregion
  }
}
