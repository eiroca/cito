// DelegatedGenerator.cs - Base class for delegate-approach generators
//
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
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci {

  public delegate bool StatementActionDelegate(ICiStatement s);
  //
  public delegate void WriteSymbolDelegate(CiSymbol statement);
  public delegate void WriteStatementDelegate(ICiStatement statement);
  public delegate void WriteExprDelegate(CiExpr expr);
  public delegate void WriteUnaryOperatorDelegate(CiUnaryExpr expr,UnaryOperatorInfo token);
  public delegate void WriteBinaryOperatorDelegate(CiBinaryExpr expr,BinaryOperatorInfo token);
  //
  public delegate void WritePropertyAccessDelegate(CiPropertyAccess expr);
  public delegate void WriteMethodDelegate(CiMethodCall method);
  //
  public delegate string TranslateSymbolNameDelegate(CiSymbol aSymbol);
  //
  public delegate TypeInfo TranslateTypeDelegate(CiType type);
  //
  public class OperatorInfo {
    public CiToken Token;
    public CiPriority Priority;
  }

  public class UnaryOperatorInfo: OperatorInfo {
    public string Prefix;
    public string Suffix;
    public WriteUnaryOperatorDelegate WriteDelegate;

    public UnaryOperatorInfo(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      this.Token = token;
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
      this.Prefix = prefix;
      this.Suffix = suffix;
    }
  }

  public class BinaryOperatorInfo: OperatorInfo {
    public string Symbol;
    public WriteBinaryOperatorDelegate WriteDelegate;

    public BinaryOperatorInfo(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      this.Token = token;
      this.Priority = priority;
      this.Symbol = symbol;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class TypeInfo {
    public CiType Type;
    public string Name;
    public string Definition;
    public bool IsNative;
    public string Null;
    public string NullInit;
    public string Init;
    public int Level;
    public string ItemType;
    public string ItemDefault;
  }

  public class UnaryOperatorMetadata {
    public Dictionary<CiToken, UnaryOperatorInfo> Metadata = new Dictionary<CiToken, UnaryOperatorInfo>();

    public UnaryOperatorMetadata() {
    }

    public void Declare(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      UnaryOperatorInfo info = new UnaryOperatorInfo(token, priority, writeDelegate, prefix, suffix);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public UnaryOperatorInfo GetUnaryOperator(CiToken token) {
      UnaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No metadata for " + token);
      }
      return result;
    }
  }

  public class BinaryOperatorMetadata {
    public Dictionary<CiToken, BinaryOperatorInfo> Metadata = new Dictionary<CiToken, BinaryOperatorInfo>();

    public BinaryOperatorMetadata() {
    }

    public void Declare(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      BinaryOperatorInfo info = new BinaryOperatorInfo(token, priority, writeDelegate, symbol);
      if (!Metadata.ContainsKey(token)) {
        Metadata.Add(token, info);
      }
      else {
        Metadata[token] = info;
      }
    }

    public BinaryOperatorInfo GetBinaryOperator(CiToken token) {
      BinaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No metadata for " + token);
      }
      return result;
    }
  }

  public class MappingMetadata<TYPE, DATA, INFO> where TYPE: class where DATA: class {
    public delegate void Delegator(DATA context);

    public class MappingData {
      public INFO Info;
      public Delegator MethodDelegate;
    }

    public Dictionary<TYPE, MappingData> Metadata = new Dictionary<TYPE, MappingData>();

    public MappingMetadata() {
    }

    public void Declare(TYPE typ, Delegator delegat) {
      if (!Metadata.ContainsKey(typ)) { 
        MappingData map = new MappingData();
        map.MethodDelegate = delegat;
        Metadata.Add(typ, map);
      }
      else {
        Metadata[typ].MethodDelegate = delegat;
      }
    }

    public void Declare(TYPE typ, INFO info, Delegator delegat) {
      if (!Metadata.ContainsKey(typ)) { 
        MappingData map = new MappingData();
        map.Info = info;
        map.MethodDelegate = delegat;
        Metadata.Add(typ, map);
      }
      else {
        MappingData map = Metadata[typ];
        map.Info = info;
        map.MethodDelegate = delegat;
      }
    }

    public bool CallDelegate(TYPE typ, DATA context) {
      MappingData info = null;
      Metadata.TryGetValue(typ, out info);
      if (info != null) {
        info.MethodDelegate(context);
      }
      return (info != null);
    }

    public INFO GetInfo(TYPE typ) {
      MappingData info = null;
      Metadata.TryGetValue(typ, out info);
      return info.Info;
    }

    public Delegator FindAppropriate(string name, Object implementer) {
      Delegator del = null;
      try {
        del = (Delegator)Delegate.CreateDelegate(typeof(Delegator), implementer, name);
      }
      catch (ArgumentNullException) {
      }
      catch (ArgumentException) {
      }
      catch (MissingMethodException) {
      }
      catch (MethodAccessException) {
      }
      return del;
    }
  }

  public class GenericMetadata<TYPE> : MappingMetadata<Type, TYPE, CiPriority> where TYPE: class {
    public MappingData GetMetadata(TYPE obj) {
      Type type = obj.GetType();
      Type baseType = type;
      bool searched = false;
      MappingData info = null;
      while (info == null) {
        Metadata.TryGetValue(type, out info);
        if (info == null) {
          type = type.BaseType;
          searched = true;
          if (type == null) {
            throw new InvalidOperationException("No metadata for " + obj);
          }
        }
      }
      if (searched) {
        Metadata.Add(baseType, info);
      }
      return info;
    }

    public CiPriority GetPriority(TYPE obj) {
      MappingData info = GetMetadata(obj);
      return info.Info;
    }

    public void Translate(TYPE obj) {
      MappingData info = GetMetadata(obj);
      info.MethodDelegate(obj);
    }
  }

  public class SymbolMapping {
    public DelegateGenerator Generator;
    public CiSymbol Symbol = null;
    public string NewName = "?";
    public SymbolMapping Parent = null;
    public List<SymbolMapping> childs = new List<SymbolMapping>();

    public SymbolMapping() {
    }

    public SymbolMapping(SymbolMapping aParent, CiSymbol aSymbol, string aNewName, DelegateGenerator Generator) {
      this.Generator = Generator;
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = aNewName;
    }

    public SymbolMapping(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck, DelegateGenerator Generator) {
      this.Generator = Generator;
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = this.GetUniqueName(inParentCheck);
    }

    public bool IsUnique(string baseName, bool inParentCheck) {
      if (Generator.IsReservedWord(baseName)) {
        return false;
      }
      SymbolMapping context = this.Parent;
      while (context != null) {
        foreach (SymbolMapping item in context.childs) {
          if (String.Compare(item.NewName, baseName, true) == 0) {
            return false;
          }
        }
        if (inParentCheck) {
          context = context.Parent;
        }
        else {
          context = null;
        }
      }
      return true;
    }

    public string GetUniqueName(bool inParentCheck) {
      if (Symbol == null) {
        return "?";
      }
      string curName = Generator.TranslateSymbolName(Symbol);
      if (!IsUnique(curName, inParentCheck)) {
        string baseName = curName;
        curName = ((baseName.StartsWith("a") ? "an" : "a")) + char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
        if (!IsUnique(curName, inParentCheck)) {
          int suffix = 1;
          do {
            curName = baseName + "__" + suffix;
            suffix++;
          }
          while (!IsUnique(curName, inParentCheck));
        }
      }
      return curName;
    }
  }

  public abstract class DelegateGenerator : BaseGenerator {
    public DelegateGenerator(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public DelegateGenerator() : base() {
      TranslateSymbolName = SymbolNameTranslator;
      InitCiLanguage();
      InitOperators();
      InitLibrary();
    }

    public override void Write(CiProgram prog) {
      PreProcess(prog);
      EmitProgram(prog);
    }

    public abstract void EmitProgram(CiProgram prog);

    #region Ci Language Translation
    protected GenericMetadata<CiSymbol> Symbols = new GenericMetadata<CiSymbol>();
    protected GenericMetadata<ICiStatement> Statemets = new GenericMetadata<ICiStatement>();
    protected GenericMetadata<CiExpr> Expressions = new GenericMetadata<CiExpr>();

    public virtual void InitCiLanguage() {
      SetSymbolTranslator(typeof(CiEnum));
      SetSymbolTranslator(typeof(CiConst));
      SetSymbolTranslator(typeof(CiField));
      SetSymbolTranslator(typeof(CiMacro));
      SetSymbolTranslator(typeof(CiMethod));
      SetSymbolTranslator(typeof(CiClass));
      SetSymbolTranslator(typeof(CiDelegate));
      //
      SetStatementTranslator(typeof(CiBlock));
      SetStatementTranslator(typeof(CiConst));
      SetStatementTranslator(typeof(CiVar));
      SetStatementTranslator(typeof(CiExpr));
      SetStatementTranslator(typeof(CiAssign));
      SetStatementTranslator(typeof(CiDelete));
      SetStatementTranslator(typeof(CiBreak));
      SetStatementTranslator(typeof(CiContinue));
      SetStatementTranslator(typeof(CiDoWhile));
      SetStatementTranslator(typeof(CiFor));
      SetStatementTranslator(typeof(CiIf));
      SetStatementTranslator(typeof(CiNativeBlock));
      SetStatementTranslator(typeof(CiReturn));
      SetStatementTranslator(typeof(CiSwitch));
      SetStatementTranslator(typeof(CiThrow));
      SetStatementTranslator(typeof(CiWhile));
      //TODO Use reflection to define the expression priority
      Expressions.Declare(typeof(CiConstExpr), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiConstExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiConstAccess), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiConstAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiVarAccess), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiVarAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiFieldAccess), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiFieldAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiPropertyAccess), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiPropertyAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiArrayAccess), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiArrayAccess", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiMethodCall), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiMethodCall", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiBinaryResourceExpr), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiBinaryResourceExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiNewExpr), CiPriority.Postfix, Expressions.FindAppropriate("Expression_CiNewExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiUnaryExpr), CiPriority.Prefix, Expressions.FindAppropriate("Expression_CiUnaryExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCondNotExpr), CiPriority.Prefix, Expressions.FindAppropriate("Expression_CiCondNotExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiPostfixExpr), CiPriority.Prefix, Expressions.FindAppropriate("Expression_CiPostfixExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCondExpr), CiPriority.CondExpr, Expressions.FindAppropriate("Expression_CiCondExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiBinaryExpr), CiPriority.Lowest, Expressions.FindAppropriate("Expression_CiBinaryExpr", this) ?? IgnoreExpr);
      Expressions.Declare(typeof(CiCoercion), CiPriority.Lowest, Expressions.FindAppropriate("Expression_CiCoercion", this) ?? IgnoreExpr);
    }

    public void IgnoreSymbol(CiSymbol symbol) {
    }

    public void SetSymbolTranslator(Type symbol) {
      string name = "Symbol_" + symbol.Name;
      SetSymbolTranslator(symbol, Symbols.FindAppropriate(name, this) ?? IgnoreSymbol);
    }

    public void SetSymbolTranslator(Type symbol, GenericMetadata<CiSymbol>.Delegator delegat) {
      Symbols.Declare(symbol, delegat);
    }

    public void Translate(CiSymbol expr) {
      Symbols.Translate(expr);
    }

    public void IgnoreStatement(ICiStatement statement) {
    }

    public void SetStatementTranslator(Type statemenent) {
      string name = "Statement_" + statemenent.Name;
      SetStatementTranslator(statemenent, Statemets.FindAppropriate(name, this) ?? IgnoreStatement);
    }

    public void SetStatementTranslator(Type statemenent, GenericMetadata<ICiStatement>.Delegator delegat) {
      Statemets.Declare(statemenent, delegat);
    }

    public void Translate(ICiStatement expr) {
      Statemets.Translate(expr);
    }

    public void IgnoreExpr(CiExpr expression) {
    }

    public void Translate(CiExpr expr) {
      Expressions.Translate(expr);
    }

    protected BinaryOperatorMetadata BinaryOperators = new BinaryOperatorMetadata();
    protected UnaryOperatorMetadata UnaryOperators = new UnaryOperatorMetadata();

    public virtual void InitOperators() {
      //TODO Use reflection to fill the structure
    }

    public void ConvertOperatorAssociative(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      GetExprType(expr);
      WriteChild(expr, expr.Left);
      Write(token.Symbol);
      WriteChild(expr, expr.Right);
    }

    public void ConvertOperatorNotAssociative(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      GetExprType(expr);
      WriteChild(expr, expr.Left);
      Write(token.Symbol);
      WriteChild(expr, expr.Right, true);
    }

    public void ConvertOperatorUnary(CiUnaryExpr expr, UnaryOperatorInfo token) {
      Write(token.Prefix);
      WriteChild(expr, expr.Inner);
      Write(token.Suffix);
    }
    #endregion

    #region Library Translation
    protected MappingMetadata<CiProperty, CiPropertyAccess, CiPriority> Properties = new MappingMetadata<CiProperty, CiPropertyAccess, CiPriority>();
    protected MappingMetadata<CiMethod, CiMethodCall, CiPriority> Methods = new MappingMetadata<CiMethod, CiMethodCall, CiPriority>();

    public virtual void InitLibrary() {
      // Properties
      SetPropertyTranslator(CiLibrary.SByteProperty);
      SetPropertyTranslator(CiLibrary.LowByteProperty);
      SetPropertyTranslator(CiLibrary.StringLengthProperty);
      // Methods
      SetMethodTranslator(CiLibrary.MulDivMethod);
      SetMethodTranslator(CiLibrary.CharAtMethod);
      SetMethodTranslator(CiLibrary.SubstringMethod);
      SetMethodTranslator(CiLibrary.ArrayCopyToMethod);
      SetMethodTranslator(CiLibrary.ArrayToStringMethod);
      SetMethodTranslator(CiLibrary.ArrayStorageClearMethod);
    }

    public void SetPropertyTranslator(CiProperty prop) {
      string name = "Library_" + prop.Name;
      SetPropertyTranslator(prop, Properties.FindAppropriate(name, this));
    }

    public void SetPropertyTranslator(CiProperty prop, MappingMetadata<CiProperty, CiPropertyAccess, CiPriority>.Delegator del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Properties.Declare(prop, del);
    }

    public void SetMethodTranslator(CiMethod met) {
      string name = "Library_" + met.Name;
      SetMethodTranslator(met, Methods.FindAppropriate(name, this));
    }

    public void SetMethodTranslator(CiMethod met, MappingMetadata<CiMethod, CiMethodCall, CiPriority>.Delegator del) {
      if (del == null) {
        throw new ArgumentNullException();
      }
      Methods.Declare(met, del);
    }

    public bool Translate(CiPropertyAccess prop) {
      return (prop.Property != null) ? Properties.CallDelegate(prop.Property, prop) : false;
    }

    public bool Translate(CiMethodCall call) {
      return (call.Method != null) ? Methods.CallDelegate(call.Method, call) : false;
    }
    #endregion

    #region Pre Processor
    public virtual bool Execute(ICiStatement[] stmt, StatementActionDelegate action) {
      if (stmt != null) {
        foreach (ICiStatement s in stmt) {
          if (Execute(s, action)) {
            return true;
          }
        }
      }
      return false;
    }

    public virtual bool Execute(ICiStatement stmt, StatementActionDelegate action) {
      if (stmt == null) {
        return false;
      }
      if (action(stmt)) {
        return true;
      }
      if (stmt is CiBlock) {
        if (Execute(((CiBlock)stmt).Statements, action)) {
          return true;
        }
      }
      else if (stmt is CiFor) {
        CiFor loop = (CiFor)stmt;
        if (Execute(loop.Init, action)) {
          return true;
        }
        if (Execute(loop.Body, action)) {
          return true;
        }
        if (Execute(loop.Advance, action)) {
          return true;
        }
      }
      else if (stmt is CiLoop) {
        CiLoop loop = (CiLoop)stmt;
        if (Execute(loop.Body, action)) {
          return true;
        }
      }
      else if (stmt is CiIf) {
        CiIf iiff = (CiIf)stmt;
        if (Execute(iiff.OnTrue, action)) {
          return true;
        }
        if (Execute(iiff.OnFalse, action)) {
          return true;
        }
      }
      else if (stmt is CiSwitch) {
        CiSwitch swith = (CiSwitch)stmt;
        foreach (CiCase cas in swith.Cases) {
          if (Execute(cas.Body, action)) {
            return true;
          }
        }
        if (Execute(swith.DefaultBody, action)) {
          return true;
        }
      }
      return false;
    }

    public virtual void PreProcess(CiProgram program) {
      ResetSymbolMapping();
      ResetClassOrder();
      ResetType();
      ResetContext();
      ResetMethodStack();
      ResetExprType();
      SymbolMapping root = new SymbolMapping();
      foreach (CiSymbol symbol in program.Globals) {
        if (!(symbol is CiClass)) {
          SymbolMapping parent = AddSymbol(root, symbol);
          if (symbol is CiEnum) {
            foreach (CiEnumValue val in ((CiEnum)symbol).Values) {
              AddSymbol(parent, val);
            }
          }
        }
      }
      foreach (CiSymbol symbol in program.Globals) {
        if (symbol is CiClass) {
          AddClass((CiClass)symbol);
        }
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        SymbolMapping parent = (klass.BaseClass != null ? FindSymbol(klass.BaseClass) : root);
        AddSymbol(parent, klass);
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        PreProcess(program, klass);
      }
    }

    public virtual void PreProcess(CiProgram program, CiClass klass) {
      SymbolMapping parent = FindSymbol(klass);
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiMethod)) {
          AddSymbol(parent, member);
        }
        if (member is CiField) {
          AddType(((CiField)member).Type);
        }
        else if (member is CiConst) {
          AddType(((CiConst)member).Type);
        }
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        AddSymbol(parent, resource);
        AddType(resource.Type);
      }
      if (klass.Constructor != null) {
        AddSymbol(parent, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          SymbolMapping methodContext = AddSymbol(parent, member, false);
          CiMethod method = (CiMethod)member;
          AddSymbol(parent, method.Signature, methodContext.NewName);
          if (method.Signature.Params.Length > 0) {
            SymbolMapping methodCall = AddSymbol(methodContext, null);
            foreach (CiParam p in method.Signature.Params) {
              AddSymbol(methodCall, p);
              AddType(p.Type);
            }
          }
          AddType(method.Signature.ReturnType);
        }
      }
      if (klass.Constructor != null) {
        PreProcess(klass, klass.Constructor);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          PreProcess(klass, (CiMethod)member);
        }
      }
    }

    public virtual void PreProcess(CiClass klass, CiMethod method) {
      Execute(method.Body, s => PreProcess(method, s));
    }

    public virtual bool PreProcess(CiMethod method, ICiStatement stmt) {
      if (stmt is CiVar) {
        CiVar v = (CiVar)stmt;
        SymbolMapping parent = FindSymbol(method);
        AddSymbol(parent, v);
        AddType(v.Type);
      }
      return false;
    }
    #endregion

    #region Symbol Mapper
    //
    public TranslateSymbolNameDelegate TranslateSymbolName { get; set; }

    protected HashSet<string> ReservedWords = null;
    protected Dictionary<CiSymbol, SymbolMapping> varMap = new  Dictionary<CiSymbol, SymbolMapping>();
    //
    public abstract string[] GetReservedWords();

    public void ResetSymbolMapping() {
      string[] words = GetReservedWords();
      if (words != null) {
        ReservedWords = new HashSet<string>(words);
      }
      else {
        ReservedWords = new HashSet<string>();
      }
      varMap.Clear();
    }

    public bool HasSymbols() {
      return varMap.Count == 0;
    }

    public bool IsReservedWord(string aName) {
      return ReservedWords.Contains(aName.ToLower());
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol) {
      return AddSymbol(aParent, aSymbol, true);
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol, bool inParentCheck) {
      SymbolMapping item = new SymbolMapping(aParent, aSymbol, inParentCheck, this);
      if (aSymbol != null) {
        try {
          varMap.Add(aSymbol, item);
        }
        catch (ArgumentException) {
          throw new ArgumentException("Symbol " + aSymbol.Name + " already added");
        }
      }
      return item;
    }

    public SymbolMapping AddSymbol(SymbolMapping aParent, CiSymbol aSymbol, string aNewName) {
      SymbolMapping item = new SymbolMapping(aParent, aSymbol, false, this);
      item.NewName = aNewName;
      if (aSymbol != null) {
        varMap.Add(aSymbol, item);
      }
      return item;
    }

    public SymbolMapping FindSymbol(CiSymbol symbol) {
      SymbolMapping result = null;
      varMap.TryGetValue(symbol, out result);
      return result;
    }

    public string SymbolNameTranslator(CiSymbol aSymbol) {
      String name = aSymbol.Name;
      if (IsReservedWord(name)) {
        name = "_" + name;
      }
      return name;
    }
    #endregion

    #region Class Order
    protected List<CiClass> classOrder = new List<CiClass>();

    public void ResetClassOrder() {
      classOrder.Clear();
    }

    public List<CiClass>GetOrderedClassList() {
      return classOrder;
    }

    public void AddClass(CiClass klass) {
      if (klass == null) {
        return;
      }
      if (classOrder.Contains(klass)) {
        return;
      }
      AddClass(klass.BaseClass);
      classOrder.Add(klass);
    }
    #endregion

    #region Type Mapper
    public TranslateTypeDelegate TranslateType { get; set; }

    protected HashSet<CiClass> refClass = new HashSet<CiClass>();
    protected HashSet<CiType> refType = new HashSet<CiType>();
    protected Dictionary<CiType, TypeInfo> TypeCache = new Dictionary<CiType, TypeInfo>();

    public HashSet<CiClass>GetClassTypeList() {
      return refClass;
    }

    public HashSet<CiType>GetTypeList() {
      return refType;
    }

    public string DecodeType(CiType type) {
      string typeDec = GetTypeInfo(type).Name;
      if (type is CiArrayStorageType) {
        //TODO handle LengthExpr
        return String.Format(typeDec, ((CiArrayStorageType)type).Length);
      }
      else {
        return typeDec;
      }
    }

    public TypeInfo GetTypeInfo(CiType type) {
      if (TypeCache.ContainsKey(type)) {
        return TypeCache[type];
      }
      TypeInfo info = TranslateType(type);
      TypeCache.Add(type, info);
      return info;
    }

    public void AddType(CiType type) {
      if (type == null) {
        return;
      }
      if (refType.Contains(type)) {
        return;
      }
      if (type is CiArrayType) {
        CiArrayType arr = (CiArrayType)type;
        if (arr.BaseType is CiClassType) {
          CiClass klass = ((CiClassType)arr.BaseType).Class;
          if (!refClass.Contains(klass)) {
            refClass.Add(klass);
          }
        }
      }
      if (type is CiClassType) {
        CiClass klass = ((CiClassType)type).Class;
        if (!refClass.Contains(klass)) {
          refClass.Add(klass);
        }
      }
      refType.Add(type);
    }

    public void ResetType() {
      refClass.Clear();
      refType.Clear();
      TypeCache.Clear();
    }
    #endregion

    #region Helper
    protected int ElemPerRow = 16;
    protected string ElemSeparator = ", ";

    public Int32 ExprAsInteger(CiExpr e, Int32 def) {
      int res = def;
      if (e is CiConstExpr) {
        object v = ((CiConstExpr)e).Value;
        if (v is Int32) {
          res = (Int32)v;
        }
        else if (v is byte) {
          res = (byte)v;
        }
      }
      return res;
    }

    public int GetArraySize(CiType type) {
      if (type is CiArrayStorageType) {
        CiArrayStorageType arr = (CiArrayStorageType)type;
        if (arr.LengthExpr == null) {
          return ((CiArrayStorageType)type).Length;
        }
      }
      return -1;
    }

    public virtual string DecodeArray(CiType type, Array array) {
      StringBuilder res = new StringBuilder();
      if (array.Length >= ElemPerRow) {
        res.Append(NewLineStr);
        OpenBlock(false);
        res.Append(GetIndentStr());
      }
      for (int i = 0; i < array.Length; i++) {
        res.Append(DecodeValue(type, array.GetValue(i)));
        if (i < (array.Length - 1)) {
          res.Append(ElemSeparator);
          if ((i + 1) % ElemPerRow == 0) {
            res.Append(NewLineStr);
            res.Append(GetIndentStr());
          }
        }
      }
      if (array.Length >= ElemPerRow) {
        CloseBlock(false);
        res.Append(NewLineStr);
        res.Append(GetIndentStr());
      }
      return res.ToString();
    }

    public virtual string DecodeValue(CiType type, object value) {
      return (value != null) ? value.ToString() : "";
    }

    public string DecodeSymbol(CiSymbol var) {
      SymbolMapping symbol = FindSymbol(var);
      return (symbol != null) ? symbol.NewName : TranslateSymbolName(var);
    }

    public virtual void WriteChild(CiExpr parent, CiExpr child) {
      WriteChild(GetPriority(parent), child, false);
    }

    public virtual void WriteChild(CiExpr parent, CiExpr child, bool nonAssoc) {
      WriteChild(GetPriority(parent), child, nonAssoc);
    }

    public virtual void WriteChild(CiPriority parentPriority, CiExpr child) {
      WriteChild(parentPriority, child, false);
    }

    public virtual void WriteChild(CiPriority parentPriority, CiExpr child, bool nonAssoc) {
      GenericMetadata<CiExpr>.MappingData exprInfo = Expressions.GetMetadata(child);
      if ((exprInfo.Info < parentPriority) || (nonAssoc && (exprInfo.Info == parentPriority))) {
        Write('(');
        exprInfo.MethodDelegate(child);
        Write(')');
      }
      else {
        exprInfo.MethodDelegate(child);
      }
    }

    public virtual CiPriority GetPriority(CiExpr expr) {
      if (expr is CiCoercion) {
        return GetPriority((CiExpr)((CiCoercion)expr).Inner);
      }
      if (expr is CiBinaryExpr) {
        return BinaryOperators.GetBinaryOperator(((CiBinaryExpr)expr).Op).Priority;
      }
      return Expressions.GetPriority(expr);
    }

    private HashSet<string> UsedFunc = new HashSet<string>();

    public void ClearUsedFunction() {
      UsedFunc.Clear();
    }

    public void UseFunction(string name) {
      if (!UsedFunc.Contains(name)) {
        UsedFunc.Add(name);
      }
    }

    public bool IsUsedFunction(string name) {
      return UsedFunc.Contains(name);
    }

    public bool HasUsedFunction() {
      return UsedFunc.Count > 0;
    }
    #endregion

    #region Context Tracker
    private Stack<int> instructions = new Stack<int>();

    public void ResetContext() {
      instructions.Clear();
    }

    public void EnterContext(int step) {
      instructions.Push(step);
    }

    public void ExitContext() {
      instructions.Pop();
    }

    public bool InContext(int inst) {
      return instructions.Any(step => (step == inst));
    }
    #endregion

    #region MethodStack
    private Stack<CiMethod> methods = new Stack<CiMethod>();

    public void ResetMethodStack() {
      methods.Clear();
    }

    public void EnterMethod(CiMethod call) {
      methods.Push(call);
    }

    public void ExitMethod() {
      methods.Pop();
    }

    public CiMethod CurrentMethod() {
      if (methods.Count > 0) {
        return methods.Peek();
      }
      return null;
    }
    #endregion

    #region ExprType
    private Dictionary<CiExpr, CiType> exprMap = new  Dictionary<CiExpr, CiType>();

    public void ResetExprType() {
      exprMap.Clear();
    }

    public CiType GetExprType(CiExpr expr) {
      CiType result;
      exprMap.TryGetValue(expr, out result);
      if (result == null) {
        result = AnalyzeExpr(expr);
        exprMap.Add(expr, result);
      }
      return result;
    }

    public CiType AnalyzeExpr(CiExpr expr) {
      if (expr == null)
        return CiType.Null;
      else if (expr is CiUnaryExpr) {
        var e = (CiUnaryExpr)expr;
        CiType t = GetExprType(e.Inner);
        return t;
      }
      else if (expr is CiPostfixExpr) {
        var e = (CiPostfixExpr)expr;
        CiType t = GetExprType(e.Inner);
        return t;
      }
      else if (expr is CiBinaryExpr) {
        var e = (CiBinaryExpr)expr;
        CiType left = GetExprType(e.Left);
        CiType right = GetExprType(e.Right);
        CiType t = ((left == null) || (left == CiType.Null)) ? right : left;
        exprMap[e.Left] = t;
        exprMap[e.Right] = t;
        return t;
      }
      else {
        return expr.Type;
      }
    }
    #endregion

  }
}
