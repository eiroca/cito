// GenJava.cs - Java code generator
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
using System.IO;

namespace Foxoft.Ci {

  public class GenJava : CiGenerator {
    public GenJava(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenJava() : base() {
      Namespace = "cito";
      TranslateSymbolName = Java_SymbolNameTranslator;
    }

    public string Java_SymbolNameTranslator(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      if (aSymbol is CiEnumValue) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiField) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiMethod) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiDelegate) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiConst) {
        name = ToUppercaseWithUnderscores(name);
      }
      else {
        name = SymbolNameTranslator(aSymbol);
      }
      return name;
    }

    public override string DecodeVisibility(CiVisibility visibility) {
      string result = "";
      switch (visibility) {
        case CiVisibility.Dead:
        case CiVisibility.Private:
          result = "private";
          break;
        case CiVisibility.Internal:
          break;
        case CiVisibility.Public:
          result = "public";
          break;
      }
      return result;
    }

    public override void EmitProgram(CiProgram prog) {
      foreach (CiSymbol symbol in prog.Globals) {
        Translate(symbol);
      }
    }

    void CreateJavaFile(CiSymbol symbol) {
      string dir = Path.GetDirectoryName(this.OutputFile);
      CreateFile(Path.Combine(dir, symbol.Name + ".java"));
      if (this.Namespace != null) {
        WriteLine("package {0};", this.Namespace);
      }
      WriteLine();
      Write(symbol.Documentation);
      Write(DecodeVisibility(symbol.Visibility));
      Write(" ");
    }

    void CloseJavaFile() {
      CloseBlock();
      CloseFile();
    }

    #region Converter Types
    public override TypeInfo Type_CiBoolType(CiType type) {
      return new TypeInfo(type, "boolean", "false");
    }

    public override TypeInfo Type_CiStringPtrType(CiType type) {
      TypeInfo result = new TypeInfo(type, "String", "null");
      result.ItemDefault = "\"\"";
      return result;
    }

    public override TypeInfo Type_CiStringStorageType(CiType type) {
      TypeInfo result = new TypeInfo(type, "String", "null");
      result.ItemDefault = "\"\"";
      return result;
    }

    public override TypeInfo Type_CiEnum(CiType type) {
      return new TypeInfo(type, "int", "0");
    }

    public override TypeInfo Type_CiClassStorageType(CiType type) {
      TypeInfo result = new TypeInfo(type, type.Name, "null");
      return result;
    }

    public override TypeInfo Type_CiClassPtrType(CiType type) {
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
      for (int i = 0; i < level; i++) {
        result.NewType = result.NewType + "[]";
      }
      return result;
    }
    #endregion

    #region Converter Symbols
    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      CreateJavaFile(enu);
      WriteFormat("interface {0} ", DecodeSymbol(enu));
      OpenBlock();
      for (int i = 0; i < enu.Values.Length; i++) {
        CiEnumValue value = enu.Values[i];
        Write(value.Documentation);
        WriteLine("int {0} = {1};", DecodeSymbol(value), i);
      }
      CloseJavaFile();
    }

    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      Write(field.Documentation);
      string qual = "";
      if (field.Type is CiClassStorageType || field.Type is CiArrayStorageType) {
        qual = "final ";
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
      WriteLine("public static final {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      WriteDoc(method);
      if (method.CallType == CiCallType.Override) {
        WriteLine("@Override");
      }
      string qual = "";
      switch (method.CallType) {
        case CiCallType.Static:
          qual = "static ";
          break;
        case CiCallType.Normal:
          if (method.Visibility != CiVisibility.Private)
            qual = "final ";
          break;
        case CiCallType.Abstract:
          qual = "abstract ";
          break;
        default:
          break;
      }
      WriteFormat("{0} {1}", DecodeVisibility(method.Visibility), qual);
      WriteSignature(method.Signature, method);
      if (method.Throws) {
        Write(" throws Exception");
      }
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
      CreateJavaFile(klass);
      OpenClass(klass.IsAbstract, klass, " extends ");
      ClearUsedFunction();
      if (klass.Constructor != null) {
        WriteFormat("public {0}()", DecodeSymbol(klass));
        Translate(klass.Constructor.Body);
      }
      foreach (CiSymbol member in klass.Members) {
        Translate(member);
      }
      if (IsUsedFunction("Substring")) {
        EmitSubstringMethod();
      }

      if (IsUsedFunction("ClearBytesMethod")) {
        EmitClearMethod("byte");
      }
      if (IsUsedFunction("ClearIntsMethod")) {
        EmitClearMethod("int");
      }
      if (klass.BinaryResources.Length > 0) {
        EmitGetBinaryResource(klass);
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("private static final {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
      }
      CloseJavaFile();
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      // TODO: doc
      CreateJavaFile(del);
      WriteFormat("interface {0} ", DecodeSymbol(del));
      OpenBlock();
      WriteSignature(del, "run");
      WriteLine(";");
      CloseJavaFile();
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      WriteFormat(".{0}", DecodeSymbol(expr.Field));
    }

    public override void Expression_CiBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      switch (expr.Op) {
        case CiToken.Equal:
        case CiToken.NotEqual:
          if (expr.Left.Type is CiStringType && !expr.Left.IsConst(null) && !expr.Right.IsConst(null)) {
            if (expr.Op == CiToken.NotEqual) {
              Write('!');
            }
            Translate(expr.Left);
            Write(".equals(");
            Translate(expr.Right);
            Write(')');
            return;
          }
          break;
        default:
          break;
      }
      base.Expression_CiBinaryExpr(expr);
    }

    public override void Expression_CiBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      WriteFormat("getBinaryResource({0}, ", DecodeSymbol(expr.Resource));
      Write(expr.Resource.Content.Length);
      Write(')');
    }

    public override void Expression_CiCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      if (expr.ResultType == CiByteType.Value && expr.OnTrue is CiConstExpr && expr.OnFalse is CiConstExpr) {
        // avoid error: possible loss of precision
        Write("(byte) (");
        base.Expression_CiCondExpr(expr);
        Write(')');
      }
      else {
        base.Expression_CiCondExpr(expr);
      }
    }

    public override void Expression_CiNewExpr(CiExpr expression) {
      CiNewExpr expr = (CiNewExpr)expression;
      CiType type = expr.NewType;
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        WriteFormat("new {0}", DecodeType(arrayType.BaseType));
        WriteInitializer(arrayType);
      }
    }

    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        Write("(byte) ");
        WriteChild(expr, (CiExpr)expr.Inner); // TODO: Assign
      }
      else if (expr.ResultType == CiIntType.Value && expr.Inner.Type == CiByteType.Value) {
        WriteChild(CiPriority.And, (CiExpr)expr.Inner); // TODO: Assign
        Write(" & 0xff");
      }
      else {
        base.Expression_CiCoercion(expr);
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

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw new Exception(");
      Translate(stmt.Message);
      WriteLine(");");
    }
    #endregion

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      Write("(byte) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_Length(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".length()");
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
      Write(".charAt(");
      Translate(expr.Arguments[0]);
      Write(')');
    }

    public override void Library_Substring(CiMethodCall expr) {
      if (expr.Arguments[0].HasSideEffect) {
        Write("substring(");
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
      Write("System.arraycopy(");
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
      Write("new String(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public override void Library_Clear(CiMethodCall expr) {
      Write("clear(");
      Translate(expr.Obj);
      Write(')');
      CiType type = ((CiArrayStorageType)expr.Obj.Type).ElementType;
      if (type == CiByteType.Value) {
        UseFunction("ClearBytesMethod");
      }
      else if (type == CiIntType.Value)
        UseFunction("ClearIntsMethod");
      else {
        throw new ArgumentException(type.Name);
      }
    }
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
      CiPropertyAccess pa = expr as CiPropertyAccess;
      if (pa != null) {
        if (pa.Property == CiLibrary.SByteProperty) {
          return GetPriority(pa.Obj);
        }
        if (pa.Property == CiLibrary.LowByteProperty) {
          return CiPriority.Prefix;
        }
      }
      else if (expr is CiCoercion) {
        CiCoercion c = (CiCoercion)expr;
        if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value) {
          return CiPriority.Prefix;
        }
        if (c.ResultType == CiIntType.Value && c.Inner.Type == CiByteType.Value) {
          return CiPriority.And;
        }
      }
      return base.GetPriority(expr);
    }

    protected void WriteDelegateCall(CiExpr expr) {
      Translate(expr);
      Write(".run");
    }

    protected override void WriteFallthrough(CiExpr expr) {
      WriteLine("//$FALL-THROUGH$");
    }

    void WriteSignature(CiDelegate del, CiSymbol symbol) {
      WriteSignature(del, DecodeSymbol(symbol));
    }

    void WriteSignature(CiDelegate del, string name) {
      WriteFormat("{0} {1}(", DecodeType(del.ReturnType), name);
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

    void EmitClearMethod(string elementType) {
      Write("private static void clear(");
      Write(elementType);
      WriteLine("[] array)");
      OpenBlock();
      WriteLine("for (int i = 0; i < array.length; i++)");
      OpenBlock(false);
      WriteLine("array[i] = 0;");
      CloseBlock(false);
      CloseBlock();
    }

    void EmitSubstringMethod() {
      WriteLine("private static String substring(String s, int offset, int length)");
      OpenBlock();
      WriteLine("return s.substring(offset, offset + length);");
      CloseBlock();
    }

    void EmitGetBinaryResource(CiClass klass) {
      WriteLine();
      WriteLine("private static byte[] getBinaryResource(String name, int length)");
      OpenBlock();
      Write("java.io.DataInputStream dis = new java.io.DataInputStream(");
      Write(klass.Name);
      WriteLine(".class.getResourceAsStream(name));");
      WriteLine("byte[] result = new byte[length];");
      Write("try ");
      OpenBlock();
      Write("try ");
      OpenBlock();
      WriteLine("dis.readFully(result);");
      CloseBlock();
      Write("finally ");
      OpenBlock();
      WriteLine("dis.close();");
      CloseBlock();
      CloseBlock();
      Write("catch (java.io.IOException e) ");
      OpenBlock();
      WriteLine("throw new RuntimeException();");
      CloseBlock();
      WriteLine("return result;");
      CloseBlock();
    }
  }
}
