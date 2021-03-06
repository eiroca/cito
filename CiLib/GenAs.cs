// GenAs.cs - ActionScript code generator
//
// Copyright (C) 2011-2014  Piotr Fusik
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
using System.IO;
using System.Text;

namespace Foxoft.Ci {

  public class GenAs : CiGenerator {
    public GenAs(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenAs() : base() {
      Namespace = "as";
      CommentContinueStr = " * ";
      CommentBeginStr = "/**";
      CommentEndStr = "*/";
      CommentCodeBegin = "<code>";
      CommentCodeEnd = "</code>";
      TranslateSymbolName = AS_SymbolNameTranslator;
    }

    public string AS_SymbolNameTranslator(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      if (aSymbol is CiConst) {
        name = ToUppercaseWithUnderscores(name);
      }
      if (aSymbol is CiEnumValue) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiMethod) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiField) {
        name = ToCamelCase(name);
      }
      else {
        name = SymbolNameTranslator(aSymbol);
      }
      return name;
    }

    void CreateAsFile(CiSymbol symbol) {
      string dir = Path.GetDirectoryName(this.OutputFile);
      CreateFile(Path.Combine(dir, DecodeSymbol(symbol) + ".as"));
      if (this.Namespace != null) {
        WriteLine("package {0}", this.Namespace);
      }
      else {
        WriteLine("package");
      }
      OpenBlock();
      WriteLine("import flash.utils.ByteArray;");
      WriteLine();
      WriteDocCode(symbol.Documentation);
      WriteVisibility(symbol);
    }

    void CloseAsFile() {
      CloseBlock(); // class
      CloseBlock(); // package
      CloseFile();
    }

    void WriteVisibility(CiSymbol symbol) {
      switch (symbol.Visibility) {
        case CiVisibility.Dead:
        case CiVisibility.Private:
          Write("private ");
          break;
        case CiVisibility.Internal:
          if (symbol.Documentation == null) {
            WriteLine("/** @private */");
          }
          Write("internal ");
          break;
        case CiVisibility.Public:
          Write("public ");
          break;
      }
    }

    public override String FormatFloat(float f) {
      String sf = f.ToString();
      if (sf.Contains("E")) {
        return sf;
      }

      if (sf.Contains(".")) {
        return sf;
      }

      return sf + ".";
    }

    public override bool WriteInit(CiType type) {
      if (type is CiClassStorageType || type is CiArrayStorageType) {
        Write(" = ");
        WriteNew(type);
        return true;
      }
      return false;
    }

    public override string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        res.AppendFormat("{0}.{1}", DecodeSymbol(ev.Type), DecodeSymbol(ev));
      }
      else if (value is Array) {
        res.Append("[ ");
        res.Append(DecodeArray(type, (Array)value));
        res.Append(" ]");
      }
      else {
        res.Append(base.DecodeValue(type, value));
      }
      return res.ToString();
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

    void WriteClearArray(CiExpr expr) {
      CiArrayStorageType array = (CiArrayStorageType)expr.Type;
      if (array.ElementType == CiBoolType.Value) {
        Write("clearArray(");
        Translate(expr);
        Write(", false)");
        UseFunction("Clear");
      }
      else if (array.ElementType == CiByteType.Value) {
        Write("clearByteArray(");
        Translate(expr);
        Write(", ");
        Write(array.Length);
        Write(')');
        UseFunction("ClearBytes");
      }
      else if (array.ElementType == CiIntType.Value) {
        Write("clearArray(");
        Translate(expr);
        Write(", 0)");
        UseFunction("Clear");
      }
      else if (array.ElementType == CiFloatType.Value) {
        Write("clearArray(");
        Translate(expr);
        Write(", 0)");
        UseFunction("Clear");
      }
      else {
        throw new ArgumentException(array.ElementType.Name);
      }
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        if (arrayType.ElementType == CiByteType.Value) {
          Write("new ByteArray()");
        }
        else {
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
    }

    void WriteBuiltins() {
      if (IsUsedFunction("Substring")) {
        WriteLine("private static function substring(s : String, offset : int, length : int) : String");
        OpenBlock();
        WriteLine("return s.substring(offset, offset + length);");
        CloseBlock();
      }
      if (IsUsedFunction("CopyArray")) {
        WriteLine("private static function copyArray(sa : ByteArray, soffset : int, da : ByteArray, doffset : int, length : int) : void");
        OpenBlock();
        WriteLine("for (var i : int = 0; i < length; i++)");
        WriteLine("\tda[doffset + i] = sa[soffset + i];");
        CloseBlock();
      }
      if (IsUsedFunction("BytesToString")) {
        WriteLine("private static function bytesToString(a : ByteArray, offset : int, length : int) : String");
        OpenBlock();
        WriteLine("var s : String = \"\";");
        WriteLine("for (var i : int = 0; i < length; i++)");
        WriteLine("\ts += String.fromCharCode(a[offset + i]);");
        WriteLine("return s;");
        CloseBlock();
      }
      if (IsUsedFunction("ClearBytes")) {
        WriteLine("private static function clearByteArray(a : ByteArray, length : int) : void");
        OpenBlock();
        WriteLine("for (var i : int = 0; i < length; i++)");
        WriteLine("\ta[i] = 0;");
        CloseBlock();
      }
      if (IsUsedFunction("Clear")) {
        WriteLine("private static function clearArray(a : Array, value : *) : void");
        OpenBlock();
        WriteLine("for (var i : int = 0; i < a.length; i++)");
        WriteLine("\ta[i] = value;");
        CloseBlock();
      }
    }

    #region Converter specialization
    public override void EmitProgram(CiProgram prog) {
      foreach (CiSymbol symbol in prog.Globals) {
        Translate(symbol);
      }
    }

    public override void InitOperators() {
      base.InitOperators();
      BinaryOperators.Declare(CiToken.Slash, CiPriority.Multiplicative, false, ConvertOperatorSlash, " / ");
    }

    public void ConvertOperatorSlash(CiBinaryExpr expr, BinaryOperatorInfo token) {
      if (expr.Type != CiFloatType.Value) {
        Write("int(");
        WriteChild(CiPriority.Multiplicative, expr.Left);
        Write(token.Symbol);
        WriteChild(CiPriority.Multiplicative, expr.Right, true);
        Write(')');
      }
      else {
        ConvertOperator(expr, token);
      }
    }
    #endregion

    #region Converter Types
    public override TypeInfo Type_CiBoolType(CiType type) {
      return new TypeInfo(type, "Boolean", "false");
    }

    public override TypeInfo Type_CiByteType(CiType type) {
      return new TypeInfo(type, "int", "0");
    }

    public override TypeInfo Type_CiIntType(CiType type) {
      return new TypeInfo(type, "int", "0");
    }

    public override TypeInfo Type_CiFloatType(CiType type) {
      return new TypeInfo(type, "Number", "0.0");
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
      TypeInfo result;
      if (type.BaseType is CiByteType) {
        result = new TypeInfo(type, "ByteArray", "null");
      }
      else {
        result = new TypeInfo(type, "Array", "null");
      }
      return result;
    }

    public override TypeInfo Type_CiArrayPtrType(CiType type) {
      TypeInfo result;
      if (type.BaseType is CiByteType) {
        result = new TypeInfo(type, "ByteArray", "null");
      }
      else {
        result = new TypeInfo(type, "Array", "null");
      }
      return result;
    }
    #endregion

    #region Converter Statements
    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      WriteFormat("var {0} : {1}", DecodeSymbol(stmt), DecodeType(stmt.Type));
      WriteInit(stmt.Type);
      if (stmt.InitialValue != null) {
        if (stmt.Type is CiArrayStorageType) {
          WriteLine(";");
          WriteClearArray(new CiVarAccess { Var = stmt });
        }
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

    #region Converter Symbols
    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      WriteDocMethod(method);
      WriteVisibility(method);
      string qual = "";
      switch (method.CallType) {
        case CiCallType.Static:
          qual = "static";
          break;
        case CiCallType.Normal:
          if (method.Visibility != CiVisibility.Private) {
            qual = "final";
          }

          break;
        case CiCallType.Override:
          qual = "override";
          break;
        default:
          break;
      }
      WriteFormat("{0} function {1}(", qual, DecodeSymbol(method));
      bool first = true;
      foreach (CiParam param in method.Signature.Params) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        WriteFormat("{0} : {1}", DecodeSymbol(param), DecodeType(param.Type));
      }
      WriteLine(") : {0}", DecodeType(method.Signature.ReturnType));
      OpenBlock();
      if (method.CallType == CiCallType.Abstract) {
        WriteLine("throw \"Abstract method called\";");
      }
      else {
        CiBlock block = method.Body as CiBlock;
        if (block != null) {
          ICiStatement[] statements = block.Statements;
          WriteCode(statements);
          if (method.Signature.ReturnType != CiType.Void && statements.Length > 0) {
            CiFor lastLoop = statements[statements.Length - 1] as CiFor;
            if (lastLoop != null && lastLoop.Cond == null) {
              WriteLine("throw \"Unreachable\";");
            }
          }
        }
        else {
          Translate(method.Body);
        }
      }
      CloseBlock();
    }

    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      CreateAsFile(enu);
      WriteLine("class {0}", DecodeSymbol(enu));
      OpenBlock();
      for (int i = 0; i < enu.Values.Length; i++) {
        CiEnumValue value = enu.Values[i];
        WriteDocCode(value.Documentation);
        WriteLine("public static const {0} : int = {1};", DecodeSymbol(value), i);
      }
      CloseAsFile();
    }

    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      WriteDocCode(field.Documentation);
      WriteVisibility(field);
      string qual = (field.Type is CiClassStorageType || field.Type is CiArrayStorageType) ? "const" : "var";
      WriteFormat("{0} {1} : {2}", qual, DecodeSymbol(field), DecodeType(field.Type));
      WriteInit(field.Type);
      WriteLine(";");
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      if (konst.Visibility != CiVisibility.Public) {
        return;
      }
      WriteDocCode(konst.Documentation);
      WriteLine("public static const {0} : {1} = {2};", DecodeSymbol(konst), DecodeType(konst.Type), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      CreateAsFile(klass);
      OpenClass(false, klass, " extends ");
      ClearUsedFunction();
      if (klass.Constructor != null) {
        WriteLine("public function {0}()", DecodeSymbol(klass));
        Translate(klass.Constructor.Body);
      }
      foreach (CiSymbol member in klass.Members) {
        Translate(member);
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteFormat("private static const {0}", DecodeSymbol(konst));
        byte[] bytes = konst.Value as byte[];
        if (bytes != null) {
          WriteLine(" : ByteArray = new ByteArray();");
          OpenBlock();
          foreach (byte b in bytes) {
            WriteLine("{0}.writeByte({1});", DecodeSymbol(konst), b);
          }
          CloseBlock();
        }
        else {
          WriteLine(" : Array = {0};", DecodeValue(konst.Type, konst.Value));
        }
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("[Embed(source=\"/{0}\", mimeType=\"application/octet-stream\")]", DecodeSymbol(resource));
        WriteLine("private static const {0}: Class;", DecodeSymbol(resource));
      }
      WriteBuiltins();
      CloseAsFile();
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
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
      WriteFormat("new {0}", DecodeSymbol(expr.Resource));
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
      Write("int(");
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
        Write("substring(");
        Translate(expr.Obj);
        Write(", ");
        Translate(expr.Arguments[0]);
        Write(", ");
        Translate(expr.Arguments[1]);
        Write(')');
        UseFunction("Substring");
      }
      else {
        Translate(expr.Obj);
        Write(".substring(");
        Translate(expr.Arguments[0]);
        Write(", ");
        Translate(new CiBinaryExpr(expr.Arguments[0], CiToken.Plus, expr.Arguments[1]));
        Write(')');
      }
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      Write("copyArray(");
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
      UseFunction("CopyArray");
    }

    public override void Library_ToString(CiMethodCall expr) {
      Write("bytesToString(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
      UseFunction("BytesToString");
    }

    public override void Library_Clear(CiMethodCall expr) {
      WriteClearArray(expr.Obj);
    }
    #endregion
  }
}
