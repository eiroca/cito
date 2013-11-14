// GenCs.cs - C# code generator
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

  public class GenCs : CiGenerator {
    public GenCs(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenCs() : base() {
      Namespace = "cito";
      CommentContinueStr = "///";
      CommentBeginStr = "";
      CommentEndStr = "";
      CommentCodeBegin = "<c>";
      CommentCodeEnd = "</c>";
    }

    protected override void Write(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDoc(text.Text);
          continue;
        }
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          switch (code.Text) {
            case "true":
              Write("<see langword=\"true\" />");
              break;
            case "false":
              Write("<see langword=\"false\" />");
              break;
            case "null":
              Write("<see langword=\"null\" />");
              break;
            default:
              Write(CommentCodeBegin);
              WriteDoc(code.Text);
              Write(CommentCodeEnd);
              break;
          }
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    protected override  void Write(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        WriteLine("/// <list type=\"bullet\">");
        foreach (CiDocPara item in list.Items) {
          Write("/// <item>");
          Write(item);
          WriteLine("</item>");
        }
        Write("/// </list>");
        WriteLine();
        Write("/// ");
        return;
      }
      Write((CiDocPara)block);
    }

    protected override void Write(CiCodeDoc doc) {
      if (doc == null) {
        return;
      }
      Write("/// <summary>");
      Write(doc.Summary);
      WriteLine("</summary>");
      if (doc.Details.Length > 0) {
        Write("/// <remarks>");
        foreach (CiDocBlock block in doc.Details) {
          Write(block);
        }
        WriteLine("</remarks>");
      }
    }

    public override string DecodeVisibility(CiVisibility visibility) {
      string res = "";
      switch (visibility) {
        case CiVisibility.Dead:
        case CiVisibility.Private:
          break;
        case CiVisibility.Internal:
          res = "internal";
          break;
        case CiVisibility.Public:
          res = "public";
          break;
      }
      return res;
    }

    #region Converter Types
    public override TypeInfo Type_CiClassStorageType(CiType type) {
      TypeInfo result = new TypeInfo(type, type.Name, "null");
      return result;
    }

    public override TypeInfo Type_CiArrayStorageType(CiType type) {
      TypeInfo baseType = GetTypeInfo(type.BaseType);
      TypeInfo result = new TypeInfo(type);
      result.Null = "null";
      result.NewType = baseType.NewType;
      result.ItemType = baseType.NewType;
      int level = type.ArrayLevel;
      for (int i=0; i<level; i++) {
        result.NewType = result.NewType + "[]";
      }
      return result;
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

    public override void Statement_CiDelete(ICiStatement statement) {
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw new System.Exception(");
      Translate(stmt.Message);
      WriteLine(");");
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        Write("(byte) ");
        WriteChild(expr, (CiExpr)expr.Inner); // TODO: Assign
      }
      else {
        base.Expression_CiCoercion(expr);
      }
    }

    public override void Expression_CiCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      WriteChild(expr, expr.Cond, true);
      Write(" ? ");
      WriteCondChild(expr, expr.OnTrue);
      Write(" : ");
      WriteCondChild(expr, expr.OnFalse);
    }
    #endregion

    #region Converter Symbols
    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      Write(enu.Documentation);
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
        Write(value.Documentation);
        Write(DecodeSymbol(value));
      }
      WriteLine();
      CloseBlock();
    }

    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      Write(field.Documentation);
      string qual = "";
      if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType) {
        qual = "readonly ";
      }
      WriteFormat("{0} {1}{2} {3}", DecodeVisibility(field.Visibility), qual, DecodeType(field.Type), DecodeSymbol(field));
      WriteInit(field.Type);
      WriteLine(";");
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      if (konst.Visibility != CiVisibility.Public) {
        return;
      }
      Write(konst.Documentation);
      WriteLine("public const {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      Write(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          WriteFormat("/// <param name=\"{0}\">", DecodeSymbol(param));
          Write(param.Documentation.Summary);
          WriteLine("</param>");
        }
      }
      string qual = "";
      switch (method.CallType) {
        case CiCallType.Static:
          qual = "static ";
          break;
        case CiCallType.Normal:
          break;
        case CiCallType.Abstract:
          qual = "abstract ";
          break;
        case CiCallType.Virtual:
          qual = "virtual ";
          break;
        case CiCallType.Override:
          qual = "override ";
          break;
      }
      WriteFormat("{0} {1}", DecodeVisibility(method.Visibility), qual);
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
      Write(klass.Documentation);
      WriteFormat("{0} ", DecodeVisibility(klass.Visibility));
      OpenClass(klass.IsAbstract, klass, " : ");
      if (klass.Constructor != null) {
        WriteFormat("public {0}() ", DecodeSymbol(klass));
        Translate(klass.Constructor.Body);
      }
      foreach (CiSymbol member in klass.Members) {
        Translate(member);
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("static readonly {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("static readonly byte[] {0} = {1};", DecodeSymbol(resource), DecodeValue(resource.Type, resource.Content));
      }
      CloseBlock();
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      Write(del.Documentation);
      WriteFormat("{0} delegate ", DecodeVisibility(del.Visibility));
      WriteSignature(del);
      WriteLine(";");
    }
    #endregion

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write("(sbyte) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      Write("(byte) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_Length(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".Length");
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      Write("(int) ((long) ");
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
      Write(".Substring(");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      Write("System.Array.Copy(");
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
    }

    public override void Library_ToString(CiMethodCall expr) {
      Write("System.Text.Encoding.UTF8.GetString(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public override void Library_Clear(CiMethodCall expr) {
      Write("System.Array.Clear(");
      Translate(expr.Obj);
      Write(", 0, ");
      Write(((CiArrayStorageType)expr.Obj.Type).Length);
      Write(')');
    }
    #endregion

    #region Converter Expression
    #endregion

    public override bool WriteInit(CiType type) {
      if (type is CiClassStorageType || type is CiArrayStorageType) {
        Write(" = ");
        WriteNew(type);
        return true;
      }
      return false;
    }

    public override CiPriority GetPriority(CiExpr expr) {
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

    void WriteCondChild(CiCondExpr condExpr, CiExpr expr) {
      // avoid error CS0172
      if (condExpr.ResultType == CiByteType.Value && expr is CiConstExpr) {
        Write("(byte) ");
      }
      WriteChild(condExpr, expr);
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        TypeInfo info = GetTypeInfo(type);
        WriteFormat("new {0}", info.ItemType);
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

    public override void WriteSignature(CiDelegate del) {
      WriteFormat("{0} {1}(", DecodeType(del.ReturnType), DecodeSymbol(del));
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

    protected override void WriteBanner() {
      base.WriteBanner();
      if (this.Namespace != null) {
        WriteFormat("namespace {0} ", this.Namespace);
        OpenBlock();
      }
    }

    protected override void WriteFooter() {
      base.WriteFooter();
      if (this.Namespace != null) {
        CloseBlock();
      }
    }
  }
}
