// CiTree.cs - Ci object model
//
// Copyright (C) 2011-2014  Piotr Fusik
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
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci {

  public enum CiPriority {
    Lowest,
    CondExpr,
    CondOr,
    CondAnd,
    Or,
    Xor,
    And,
    Equality,
    Ordering,
    Shift,
    Additive,
    Multiplicative,
    Prefix,
    Postfix,
    Highest
  }

  public enum CiVisibility {
    Dead,
    Private,
    Internal,
    Public
  }

  public enum PtrWritability {
    Unknown,
    ReadOnly,
    ReadWrite
  }

  public enum CiCallType {
    Static,
    Normal,
    Abstract,
    Virtual,
    Override
  }

  public enum CiWriteStatus {
    NotYet,
    InProgress,
    Done
  }

  public interface ICiSymbolVisitor {
    void Visit(CiEnum symbol);

    void Visit(CiConst symbol);

    void Visit(CiField symbol);

    void Visit(CiMethod symbol);

    void Visit(CiClass symbol);

    void Visit(CiDelegate symbol);
  }

  public interface ICiTypeVisitor {
    CiType Visit(CiUnknownType type);

    CiType Visit(CiStringStorageType type);

    CiType Visit(CiClassType type);

    CiType Visit(CiArrayType type);

    CiType Visit(CiArrayStorageType type);

    CiType Visit(CiDelegate type);
  }

  public interface ICiStatementVisitor {
    void Visit(CiBlock statement);

    void Visit(CiConst statement);

    void Visit(CiVar statement);

    void Visit(CiExpr statement);

    void Visit(CiAssign statement);

    void Visit(CiDelete statement);

    void Visit(CiBreak statement);

    void Visit(CiContinue statement);

    void Visit(CiDoWhile statement);

    void Visit(CiFor statement);

    void Visit(CiIf statement);

    void Visit(CiNativeBlock statement);

    void Visit(CiReturn statement);

    void Visit(CiSwitch statement);

    void Visit(CiThrow statement);

    void Visit(CiWhile statement);
  }

  public interface ICiExprVisitor {
    CiExpr Visit(CiSymbolAccess expr);

    CiExpr Visit(CiVarAccess expr);

    CiExpr Visit(CiUnknownMemberAccess expr);

    CiExpr Visit(CiIndexAccess expr);

    CiExpr Visit(CiMethodCall expr);

    CiExpr Visit(CiUnaryExpr expr);

    CiExpr Visit(CiCondNotExpr expr);

    CiExpr Visit(CiPostfixExpr expr);

    CiExpr Visit(CiBinaryExpr expr);

    CiExpr Visit(CiBoolBinaryExpr expr);

    CiExpr Visit(CiCondExpr expr);

    CiExpr Visit(CiBinaryResourceExpr expr);

    CiExpr Visit(CiNewExpr expr);
  }

  public interface ICiPtrType {
    PtrWritability Writability { 
      get; 
      set;
    }

    HashSet<ICiPtrType> Sources { 
      get;
    }
  }

  public interface ICiStatement {
    bool CompletesNormally {
      get;
    }

    void Accept(ICiStatementVisitor v);
  }

  public class CiLibrary {
    public static readonly CiProperty LowByteProperty = new CiProperty("LowByte", CiByteType.Value);
    public static readonly CiProperty SByteProperty = new CiProperty("SByte", CiIntType.Value);
    public static readonly CiProperty StringLengthProperty = new CiProperty("Length", CiIntType.Value);
    public static readonly CiMethod MulDivMethod = new CiMethod("MulDiv", CiIntType.Value, new CiParam("numerator", CiIntType.Value), new CiParam("denominator", CiIntType.Value));
    public static readonly CiMethod CharAtMethod = new CiMethod("CharAt", CiIntType.Value, new CiParam("index", CiIntType.Value));
    public static readonly CiMethod SubstringMethod = new CiMethod("Substring", CiStringPtrType.Value, new CiParam("startIndex", CiIntType.Value), new CiParam("length", CiIntType.Value));
    public static readonly CiMethod ArrayCopyToMethod = new CiMethod("CopyTo", CiType.Void, new CiParam("sourceIndex", CiIntType.Value), new CiParam("destinationArray", CiArrayPtrType.WritableByteArray), new CiParam("destinationIndex", CiIntType.Value), new CiParam("length", CiIntType.Value));
    public static readonly CiMethod ArrayToStringMethod = new CiMethod("ToString", CiStringPtrType.Value, new CiParam("startIndex", CiIntType.Value), new CiParam("length", CiIntType.Value));
    public static readonly CiMethod ArrayStorageClearMethod = new CiMethod("Clear", CiType.Void) { IsMutator = true };
  }

  public class CiComment {
    public List<string> Comments;

    public CiComment() {
      this.Comments = new List<string>();
    }

    public CiComment(List<string> comments, bool clone = true) {
      if (clone) {
        this.Comments = new List<string>(comments);
      }
      else {
        this.Comments = comments;
      }
    }

    public void Add(string comment) {
      Comments.Add(comment ?? "");
    }
  }

  public abstract class CiDocInline {
  }

  public class CiDocText : CiDocInline {
    public string Text;

    public CiDocText(string text) {
      this.Text = text;
    }
  }

  public class CiDocCode : CiDocInline {
    public string Text;

    public CiDocCode(string text) {
      this.Text = text;
    }
  }

  public abstract class CiDocBlock {
  }

  public class CiDocPara : CiDocBlock {
    public CiDocInline[] Children;

    public CiDocPara(CiDocInline[] children) {
      this.Children = children;
    }
  }

  public class CiDocList : CiDocBlock {
    public CiDocPara[] Items;

    public  CiDocList(CiDocPara[] items) {
      this.Items = items;
    }
  }

  public class CiCodeDoc {
    public CiDocPara Summary;
    public CiDocBlock[] Details;

    public  CiCodeDoc(CiDocPara summary, CiDocBlock[] details) {
      this.Summary = summary;
      this.Details = details;
    }
  }

  public abstract class CiSymbol {
    public CodePosition Position;
    public CiCodeDoc Documentation;
    public CiVisibility Visibility;
    public string Name;

    public CiSymbol(CodePosition p, string name) {
      this.Position = p;
      this.Name = name;
    }

    public virtual void Accept(ICiSymbolVisitor v) {
      throw new NotImplementedException(this.ToString());
    }
  }

  public class CiUnknownSymbol : CiSymbol {
    public CiUnknownSymbol(CodePosition p, string name) : base(p, name) {
    }
  }

  public class CiType : CiSymbol {
    public static readonly CiType Null = new CiType("null");
    public static readonly CiType Void = new CiType("void");

    public virtual Type DotNetType { 
      get { 
        throw new NotSupportedException("No corresponding .NET type");
      }
    }

    public virtual CiType BaseType { 
      get { 
        return this;
      }
    }

    public virtual int ArrayLevel { 
      get {
        return 0;
      }
    }

    public virtual CiType Ptr { 
      get { 
        return null;
      }
    }

    public virtual CiClass StorageClass { 
      get {
        return null; 
      }
    }

    public CiType(CodePosition p, string name) : base(p, name) {
    }

    public CiType(string name) : base(null, name) {
    }

    public virtual CiSymbol LookupMember(string name) {
      throw new ParseException(Position, "{0} has no members", this.GetType());
    }

    public virtual CiType Accept(ICiTypeVisitor v) {
      return this;
    }

    public virtual bool Equals(CiType obj) {
      return this == obj;
    }
  }

  public class CiUnknownType : CiType {
    public CiUnknownType(CodePosition p, string name) : base(p, name) {
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiBoolType : CiType {
    public static readonly CiBoolType Value = new CiBoolType(null, "bool");

    public CiBoolType(CodePosition p, string name) : base(p, name) {
    }

    public override Type DotNetType { 
      get { 
        return typeof(bool);
      }
    }
  }

  public class CiByteType : CiType {
    public static readonly CiByteType Value = new CiByteType("byte");

    public CiByteType(string name) : base(null, name) {
    }

    public CiByteType(CodePosition p, string name) : base(p, name) {
    }

    public override Type DotNetType {
      get { 
        return typeof(byte); 
      } 
    }

    public override CiSymbol LookupMember(string name) {
      switch (name) {
        case "SByte":
          return CiLibrary.SByteProperty;
        default:
          throw new ParseException(Position, "No member {0} in byte", name);
      }
    }
  }

  public class CiIntType : CiType {
    public static readonly CiIntType Value = new CiIntType("int");

    public CiIntType(string name) : base(null, name) {
    }

    public CiIntType(CodePosition p, string name) : base(p, name) {
    }

    public override Type DotNetType { 
      get { 
        return typeof(int);
      }
    }

    public override CiSymbol LookupMember(string name) {
      switch (name) {
        case "LowByte":
          return CiLibrary.LowByteProperty;
        case "MulDiv":
          return CiLibrary.MulDivMethod;
        default:
          throw new ParseException(Position, "No member {0} in int", name);
      }
    }
  }

  public abstract class CiStringType : CiType {
    public CiStringType(CodePosition p, string name) : base(p, name) {
    }

    public override Type DotNetType { 
      get { 
        return typeof(string);
      }
    }

    public override CiSymbol LookupMember(string name) {
      switch (name) {
        case "Length":
          return CiLibrary.StringLengthProperty;
        case "Substring":
          // CharAt is available only via bracket indexing
          return CiLibrary.SubstringMethod;
        default:
          throw new ParseException(Position, "No member {0} in string", name);
      }
    }
  }

  public class CiStringPtrType : CiStringType {
    public static readonly CiStringPtrType Value = new CiStringPtrType("string");

    public CiStringPtrType(string name) : base(null, name) {
    }

    public CiStringPtrType(CodePosition p, string name) : base(p, name) {
    }
  }

  public class CiStringStorageType : CiStringType {
    public CiExpr LengthExpr;
    public int Length;

    public CiStringStorageType(CodePosition p, string name) : base(p, name) {
    }

    public override bool Equals(CiType obj) {
      CiStringStorageType that = obj as CiStringStorageType;
      return that != null && this.Length == that.Length;
    }

    public override CiType Ptr { 
      get {
        return CiStringPtrType.Value;
      }
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }
  }

  public abstract class CiClassType : CiType {
    public CiClass Class;

    public CiClassType(CodePosition p, string name) : base(p, name) {
    }

    public override CiSymbol LookupMember(string name) {
      return this.Class.Members.Lookup(Position, name);
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiClassPtrType : CiClassType, ICiPtrType {
    readonly HashSet<ICiPtrType> _sources = new HashSet<ICiPtrType>();
    PtrWritability _writability = PtrWritability.Unknown;

    public CiClassPtrType(CodePosition p, string name, CiClass klass) : base(p, name) {
      this.Class = klass;
    }

    public PtrWritability Writability { 
      get {
        return this._writability;
      } 
      set { 
        this._writability = value;
      }
    }

    public HashSet<ICiPtrType> Sources { 
      get { 
        return this._sources;
      }
    }

    public override bool Equals(CiType obj) {
      CiClassPtrType that = obj as CiClassPtrType;
      return that != null && this.Class == that.Class;
    }
  }

  public class CiClassStorageType : CiClassType {
    public CiClassStorageType(CodePosition p, string name, CiClass klass) : base(p, name) {
      this.Class = klass;
    }

    public override bool Equals(CiType obj) {
      CiClassStorageType that = obj as CiClassStorageType;
      return that != null && this.Class == that.Class;
    }

    public override CiType Ptr { 
      get { 
        return new CiClassPtrType(Position, Name, this.Class);
      }
    }

    public override CiClass StorageClass { 
      get { 
        return this.Class;
      }
    }
  }

  public abstract class CiArrayType : CiType {
    public CiType ElementType;

    public CiArrayType(CodePosition p, string name, CiType elementType) : base(p, name) {
      this.ElementType = elementType;
    }

    public override CiType BaseType { 
      get { 
        return this.ElementType.BaseType;
      }
    }

    public override int ArrayLevel { 
      get { 
        return 1 + this.ElementType.ArrayLevel;
      }
    }

    public override CiSymbol LookupMember(string name) {
      switch (name) {
        case "CopyTo":
          if (this.ElementType == CiByteType.Value) return CiLibrary.ArrayCopyToMethod;
          throw new ParseException(Position, "CopyTo available only for byte arrays");
        case "ToString":
          if (this.ElementType == CiByteType.Value) return CiLibrary.ArrayToStringMethod;
          throw new ParseException(Position, "ToString available only for byte arrays");
        default:
          throw new ParseException(Position, "No member {0} in array", name);
      }
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }

    public override CiClass StorageClass { 
      get { 
        return this.ElementType.StorageClass;
      }
    }
  }

  public class CiArrayPtrType : CiArrayType, ICiPtrType {
    PtrWritability _writability = PtrWritability.Unknown;
    readonly HashSet<ICiPtrType> _sources = new HashSet<ICiPtrType>();
    public static readonly CiArrayPtrType WritableByteArray = new CiArrayPtrType(null, null, CiByteType.Value);

    public CiArrayPtrType(CodePosition p, string name, CiType elementType) : base(p, name, elementType) {
    }

    public PtrWritability Writability { 
      get { 
        return this._writability;
      } 
      set { 
        this._writability = value;
      }
    }

    public HashSet<ICiPtrType> Sources {
      get {
        return this._sources;
      }
    }

    public override bool Equals(CiType obj) {
      CiArrayPtrType that = obj as CiArrayPtrType;
      return that != null && this.ElementType.Equals(that.ElementType);
    }
  }

  public class CiArrayStorageType : CiArrayType {
    public CiExpr LengthExpr;
    public int Length;

    public CiArrayStorageType(CodePosition p, string name, CiType elementType, int leght) : base(p, name, elementType) {
      this.Length = leght;
    }

    public CiArrayStorageType(CodePosition p, string name, CiType elementType, CiExpr lengthExpr) : base(p, name, elementType) {
      this.LengthExpr = lengthExpr;
    }

    public override CiType Ptr { 
      get { 
        return new CiArrayPtrType(null, null, this.ElementType);
      }
    }

    public override CiSymbol LookupMember(string name) {
      switch (name) {
        case "Clear":
          if (this.ElementType == CiByteType.Value || this.ElementType == CiIntType.Value) return CiLibrary.ArrayStorageClearMethod;
          throw new ParseException(Position, "Clear available only for byte and int arrays");
        case "Length":
          return new CiConst(null, null, CiIntType.Value, this.Length);
        default:
          return base.LookupMember(name);
      }
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiEnumValue : CiSymbol {
    public CiEnum Type;

    public CiEnumValue(CodePosition p) : base(p, null) {
    }
  }

  public class CiEnum : CiType {
    public CiEnumValue[] Values;

    public CiEnum(CodePosition p, string name) : base(p, name) {
    }

    public override CiSymbol LookupMember(string name) {
      CiEnumValue value = this.Values.SingleOrDefault(v => v.Name == name);
      if (value == null) {
        throw new ParseException(Position, "{0} not found in enum {1}", name, this.Name);
      }
      return value;
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }
  }

  public class CiTypedSymbol : CiSymbol {
    public CiTypedSymbol(CodePosition p, string name) : base(p, name) {
    }

    public CiType Type;
  }

  public class CiField : CiTypedSymbol {
    public CiClass Class;

    public CiField(CodePosition p, string name, CiClass klass, CiType type) : base(p, name) {
      this.Class = klass;
      this.Type = type;
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }
  }

  public class CiProperty : CiTypedSymbol {
    public CiProperty(string name, CiType type) : base(null, name) {
      this.Type = type;
    }

    public CiProperty(CodePosition p, string name, CiType type) : base(p, name) {
      this.Type = type;
    }

    public override void Accept(ICiSymbolVisitor v) {
    }
  }

  public class CiConst : CiTypedSymbol, ICiStatement {
    public CiClass Class;
    public object Value;
    public string GlobalName;
    public bool Is7Bit;
    public bool CurrentlyResolving;

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public CiConst(CodePosition p, string name, CiType type, object value) : base(p, name) {
      this.Type = type;
      this.Value = value;
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiVar : CiTypedSymbol, ICiStatement {
    public CiExpr InitialValue;
    // C89 only
    public bool WriteInitialValue;

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public CiVar(CodePosition p, string name) : base(p, name) {
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiBinaryResource : CiSymbol {
    public byte[] Content;
    public CiArrayStorageType Type;

    public CiBinaryResource(CodePosition p, string name) : base(p, name) {
    }
  }

  public class CiParam : CiVar {
    public CiParam(CodePosition p, string name, CiType type) : base(p, name) {
      this.Type = type;
    }

    public CiParam(string name, CiType type) : base(null, name) {
      this.Type = type;
    }

    public CiParam(CodePosition p, string name, CiType type, CiCodeDoc docs) : base(p, name) {
      this.Type = type;
      this.Documentation = docs;
    }
  }

  public abstract class CiMaybeAssign {
    public abstract CiType Type {
      get;
    }
  }

  public abstract class CiExpr : CiMaybeAssign {
    public CodePosition Position;

    public virtual bool IsConst(object value) {
      return false;
    }

    public abstract bool HasSideEffect {
      get;
    }

    public virtual CiExpr Accept(ICiExprVisitor v) {
      return this;
    }
  }

  public class CiConstExpr : CiExpr {
    public object Value;

    public CiConstExpr(object value) {
      this.Value = value;
    }

    public CiConstExpr(int value) {
      this.Value = value >= 0 && value <= 255 ? (byte)value : (object)value;
    }

    public override CiType Type {
      get {
        if (this.Value is bool) return CiBoolType.Value;
        if (this.Value is byte) return CiByteType.Value;
        if (this.Value is int) return CiIntType.Value;
        if (this.Value is string) return CiStringPtrType.Value;
        if (this.Value is CiEnumValue) return ((CiEnumValue)this.Value).Type;
        if (this.Value == null) return CiType.Null;
        throw new NotImplementedException();
      }
    }

    public override bool IsConst(object value) {
      return object.Equals(this.Value, value);
    }

    public override bool HasSideEffect {
      get {
        return false;
      }
    }

    public override string ToString() {
      return this.Value == null ? "null" : this.Value.ToString();
    }
  }

  public abstract class CiLValue : CiExpr {
  }

  public class CiSymbolAccess : CiExpr {
    public CiSymbol Symbol;

    public override CiType Type {
      get {
        throw new NotSupportedException();
      }
    }

    public override bool HasSideEffect {
      get {
        throw new NotSupportedException();
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public override string ToString() {
      return this.Symbol.Name;
    }

  }

  public class CiConstAccess : CiExpr {
    public CiConst Const;

    public override CiType Type {
      get {
        return this.Const.Type;
      }
    }

    public override bool HasSideEffect {
      get {
        return false;
      }
    }

  }

  public class CiVarAccess : CiLValue {
    public CiVar Var;

    public override CiType Type {
      get {
        return this.Var.Type;
      }
    }

    public override bool HasSideEffect {
      get {
        return false;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiUnknownMemberAccess : CiExpr {
    public CiExpr Parent;
    public string Name;

    public override CiType Type {
      get {
        throw new NotSupportedException();
      }
    }

    public override bool HasSideEffect {
      get {
        throw new NotSupportedException();
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public override string ToString() {
      return this.Parent + "." + this.Name;
    }

  }

  public class CiFieldAccess : CiLValue {
    public CiExpr Obj;
    public CiField Field;

    public override CiType Type {
      get {
        return this.Field.Type;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Obj.HasSideEffect;
      }
    }


  }

  public class CiPropertyAccess : CiExpr {
    public CiExpr Obj;
    public CiProperty Property;

    public override CiType Type {
      get {
        return this.Property.Type;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Obj.HasSideEffect;
      }
    }
  }

  public class CiIndexAccess : CiExpr {
    public CiExpr Parent;
    public CiExpr Index;

    public override CiType Type {
      get {
        throw new NotSupportedException();
      }
    }

    public override bool HasSideEffect {
      get {
        throw new NotSupportedException();
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public override string ToString() {
      return this.Parent + "[" + this.Index + "]";
    }
  }

  public class CiArrayAccess : CiLValue {
    public CiExpr Array;
    public CiExpr Index;

    public override CiType Type {
      get {
        return ((CiArrayType)this.Array.Type).ElementType;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Array.HasSideEffect || this.Index.HasSideEffect;
      }
    }
  }

  public class CiMethodCall : CiExpr, ICiStatement {
    public CiExpr Obj;
    public CiMethod Method;
    public CiExpr[] Arguments;

    public CiDelegate Signature {
      get {
        return this.Method != null ? this.Method.Signature : (CiDelegate)this.Obj.Type;
      }
    }

    public override CiType Type {
      get {
        return this.Signature.ReturnType;
      }
    }

    public override bool HasSideEffect {
      get {
        return true;
      }
    }

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiUnaryExpr : CiExpr {
    public CiToken Op;
    public CiExpr Inner;

    public override CiType Type {
      get {
        return CiIntType.Value;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Op == CiToken.Increment || this.Op == CiToken.Decrement || this.Inner.HasSideEffect;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiCondNotExpr : CiExpr {
    public CiExpr Inner;

    public override CiType Type {
      get {
        return CiBoolType.Value;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Inner.HasSideEffect;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiPostfixExpr : CiExpr, ICiStatement {
    public CiExpr Inner;
    public CiToken Op;

    public override CiType Type {
      get {
        return CiIntType.Value;
      }
    }

    public override bool HasSideEffect {
      get {
        return true;
      }
    }

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiBinaryExpr : CiExpr {
    public CiExpr Left;
    public CiToken Op;
    public CiExpr Right;

    public string OpString {
      get {
        switch (this.Op) {
          case CiToken.Plus:
            return "+";
          case CiToken.Minus:
            return "-";
          case CiToken.Asterisk:
            return "*";
          case CiToken.Slash:
            return "/";
          case CiToken.Mod:
            return "%";
          case CiToken.ShiftLeft:
            return "<<";
          case CiToken.ShiftRight:
            return ">>";
          case CiToken.Less:
            return "<";
          case CiToken.LessOrEqual:
            return "<=";
          case CiToken.Greater:
            return ">";
          case CiToken.GreaterOrEqual:
            return ">=";
          case CiToken.Equal:
            return "==";
          case CiToken.NotEqual:
            return "!=";
          case CiToken.And:
            return "&";
          case CiToken.Or:
            return "|";
          case CiToken.Xor:
            return "^";
          case CiToken.CondAnd:
            return "&&";
          case CiToken.CondOr:
            return "||";
          default:
            throw new ArgumentException(this.Op.ToString());
        }
      }
    }

    public CiBinaryExpr(CiExpr Left, CiToken Op, CiExpr Right) {
      this.Left = Left;
      this.Op = Op;
      this.Right = Right;
    }

    public override CiType Type {
      get {
        return CiIntType.Value;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Left.HasSideEffect || this.Right.HasSideEffect;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public override string ToString() {
      return "(" + this.Left + " " + this.OpString + " " + this.Right + ")";
    }
  }

  public class CiBoolBinaryExpr : CiBinaryExpr {
    public CiBoolBinaryExpr(CiExpr Left, CiToken Op, CiExpr Right) : base(Left, Op, Right) {
    }

    public override CiType Type {
      get {
        return CiBoolType.Value;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiCondExpr : CiExpr {
    public CiExpr Cond;
    public CiType ResultType;
    public CiExpr OnTrue;
    public CiExpr OnFalse;

    public override CiType Type {
      get {
        return this.ResultType;
      }
    }

    public override bool HasSideEffect {
      get {
        return this.Cond.HasSideEffect || this.OnTrue.HasSideEffect || this.OnFalse.HasSideEffect;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }

    public override string ToString() {
      return "(" + this.Cond + " ? " + this.OnTrue + " : " + this.OnFalse + ")";
    }

  }

  public class CiBinaryResourceExpr : CiExpr {
    public CiExpr NameExpr;
    public CiBinaryResource Resource;

    public override CiType Type {
      get {
        return this.Resource.Type;
      }
    }

    public override bool HasSideEffect {
      get {
        return false;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiNewExpr : CiExpr {
    public CiType NewType;

    public override CiType Type {
      get {
        return this.NewType.Ptr;
      }
    }

    public override bool HasSideEffect {
      get {
        return true;
      }
    }

    public override CiExpr Accept(ICiExprVisitor v) {
      return v.Visit(this);
    }
  }

  public class CiCoercion : CiExpr {
    public CiType ResultType;
    public CiMaybeAssign Inner;

    public override CiType Type {
      get {
        return this.ResultType;
      }
    }

    public override bool HasSideEffect {
      get {
        return ((CiExpr)this.Inner).HasSideEffect;
      }
    }
    // TODO: Assign
  }

  public class CiAssign : CiMaybeAssign, ICiStatement {
    public CiExpr Target;
    public CiToken Op;
    public CiMaybeAssign Source;

    public override CiType Type {
      get {
        return this.Target.Type;
      }
    }

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiDelete : ICiStatement {
    public CiExpr Expr;

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public abstract class CiCondCompletionStatement : ICiStatement {
    public bool CompletesNormally {
      get;
      set;
    }

    public abstract void Accept(ICiStatementVisitor v);
  }

  public abstract class CiLoop : CiCondCompletionStatement {
    public CiExpr Cond;
    public ICiStatement Body;
  }

  public class CiBlock : CiCondCompletionStatement {
    public ICiStatement[] Statements;

    public CiBlock(ICiStatement[] Statements) {
      this.Statements = Statements;
    }

    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiBreak : ICiStatement {
    public bool CompletesNormally {
      get {
        return false;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiContinue : ICiStatement {
    public bool CompletesNormally {
      get {
        return false;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiDoWhile : CiLoop {
    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiFor : CiLoop {
    public SymbolTable Symbols;
    public ICiStatement Init;
    public ICiStatement Advance;

    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiIf : CiCondCompletionStatement {
    public CiExpr Cond;
    public ICiStatement OnTrue;
    public ICiStatement OnFalse;

    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiNativeBlock : ICiStatement {
    public string Content;

    public bool CompletesNormally {
      get {
        return true;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiReturn : ICiStatement {
    public CiExpr Value;

    public bool CompletesNormally {
      get {
        return false;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiCase {
    public object[] Values;
    public ICiStatement[] Body;
    public bool Fallthrough;
    public CiExpr FallthroughTo;
  }

  public class CiSwitch : CiCondCompletionStatement {
    public CiExpr Value;
    public CiCase[] Cases;
    public ICiStatement[] DefaultBody;

    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiThrow : ICiStatement {
    public CiExpr Message;

    public bool CompletesNormally {
      get {
        return false;
      }
    }

    public void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiWhile : CiLoop {
    public override void Accept(ICiStatementVisitor v) {
      v.Visit(this);
    }
  }

  public class CiDelegate : CiType {
    public CiType ReturnType;
    public CiParam[] Params;
    // C only
    public CiWriteStatus WriteStatus;

    public CiDelegate(CodePosition p, string name) : base(p, name) {
    }

    public override CiType Accept(ICiTypeVisitor v) {
      return v.Visit(this);
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }
  }

  public class CiMethod : CiSymbol {
    public CiClass Class;
    public CiCallType CallType;
    public CiDelegate Signature;
    public CiParam This;
    public ICiStatement Body;
    public bool Throws;
    public object ErrorReturnValue;
    public readonly HashSet<CiMethod> CalledBy = new HashSet<CiMethod>();
    public readonly HashSet<CiMethod> Calls = new HashSet<CiMethod>();
    public bool IsMutator;

    public CiMethod(CodePosition p, string name, CiType returnType, params CiParam[] paramz) : base(p, name) {
      this.CallType = CiCallType.Normal;
      this.Signature = new CiDelegate(p, name) { ReturnType = returnType, Params = paramz };
    }

    public CiMethod(string name, CiType returnType, params CiParam[] paramz) : this(null, name, returnType, paramz) {
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }
  }

  public class CiClass : CiSymbol {
    public bool IsAbstract;
    public CiClass BaseClass;
    public SymbolTable Members;
    public CiMethod Constructor;
    public CiConst[] ConstArrays;
    public CiBinaryResource[] BinaryResources;
    public bool IsResolved;
    public string SourceFilename;
    // C, JS only
    public CiWriteStatus WriteStatus;
    // C only
    public bool HasFields;
    // C only
    public bool Constructs;
    // C only
    public bool IsAllocated;

    public CiClass(CodePosition p, string name) : base(p, name) {
    }

    public override void Accept(ICiSymbolVisitor v) {
      v.Visit(this);
    }
  }

  public class CiUnknownClass : CiClass {
    public CiUnknownClass(CodePosition p, string name) : base(p, name) {
    }
  }

  public class CiProgram {
    public CiComment GlobalComment;
    public SymbolTable Globals;

    public CiProgram(CiComment globalComment, SymbolTable globals) {
      this.GlobalComment = globalComment;
      this.Globals = globals;
    }
  }
}