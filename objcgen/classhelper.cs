﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Embeddinator.ObjC {

	public class ClassHelper {

		protected SourceWriter headers;
		protected SourceWriter private_headers;
		protected SourceWriter implementation;

		public ClassHelper (SourceWriter headers, SourceWriter implementation, SourceWriter privateHeaders = null)
		{
			this.headers = headers;
			this.implementation = implementation;
			this.private_headers = privateHeaders;
		}

		public bool IsBaseTypeBound { get; set; }
		public bool IsStatic { get; set; }

		public string AssemblyQualifiedName { get; set; }
		public string AssemblyName { get; set; }
		public string BaseTypeName { get; set; }
		public string Name { get; set; }
		public string Namespace { get; set; }
		public string ManagedName { get; set; }

		public int MetadataToken { get; set; }
		public List<string> Protocols { get; set; }

		public virtual void BeginHeaders ()
		{
			headers.WriteLine ();
			headers.WriteLine ($"/** Class {Name}");
			headers.WriteLine ($" *  Corresponding .NET Qualified Name: `{AssemblyQualifiedName}`");
			headers.WriteLine (" */");
			headers.Write ($"@interface {Name} : {BaseTypeName}");
			var pcount = Protocols.Count;
			if (pcount > 0) {
				headers.Write (" <");
				for (int i = 0; i < pcount; i++) {
					if (i > 0)
						headers.Write (", ");
					headers.Write (Protocols [i]);
				}
				headers.Write (">");
			}
			headers.WriteLine (" {");
			if (!IsStatic && !IsBaseTypeBound) {
				headers.Indent++;
				headers.WriteLine ("// This field is not meant to be accessed from user code");
				headers.WriteLine ("@public MonoEmbedObject* _object;");
				headers.Indent--;
			}
			headers.WriteLine ("}");
			headers.WriteLine ();
		}

		public void DefineNoDefaultInit ()
		{
			if (IsStatic) {
				headers.WriteLine ("/** This is a static type and no instance can be initialized");
			} else {
				headers.WriteLine ("/** This type is not meant to be created using only default values");
			}
			headers.WriteLine (" *  Both the `-init` and `+new` selectors cannot be used to create instances of this type.");
			headers.WriteLine (" */");
			headers.WriteLine ("- (nullable instancetype)init NS_UNAVAILABLE;");
			headers.WriteLine ("+ (nullable instancetype)new NS_UNAVAILABLE;");
			headers.WriteLine ();
		}

		protected void DefineInitForSuper ()
		{
			headers.WriteLine ("/** This selector is not meant to be called from user code");
			headers.WriteLine (" *  It exists solely to allow the correct subclassing of managed (.net) types");
			headers.WriteLine (" */");
			headers.WriteLine ("- (nullable instancetype)initForSuper;");
			headers.WriteLine ();
		}

		public virtual void EndHeaders ()
		{
			if (!IsStatic)
				DefineInitForSuper ();
			headers.WriteLine ("@end");
			headers.WriteLine ();
		}

		public virtual void BeginImplementation (string implementationName = null)
		{
			var name = implementationName ?? Name;
			implementation.WriteLine ();
			implementation.WriteLine ($"@implementation {name} {{");
			implementation.WriteLine ("}");
			implementation.WriteLine ();
			WriteInitialize (name);
			WriteDealloc ();
		}

		void WriteInitialize (string name)
		{
			implementation.WriteLine ("+ (void) initialize");
			implementation.WriteLine ("{");
			implementation.Indent++;
			implementation.WriteLine ($"if (self != [{name} class])");
			implementation.Indent++;
			implementation.WriteLine ("return;");
			implementation.Indent--;
			implementation.WriteLine ($"__lookup_assembly_{AssemblyName} ();");

			implementation.WriteLineUnindented ("#if TOKENLOOKUP");
			implementation.WriteLine ($"{Name}_class = mono_class_get (__{AssemblyName}_image, 0x{MetadataToken:X8});");
			implementation.WriteLineUnindented ("#else");
			implementation.WriteLine ($"{Name}_class = mono_class_from_name (__{AssemblyName}_image, \"{Namespace}\", \"{ManagedName}\");");
			implementation.WriteLineUnindented ("#endif");
			implementation.Indent--;
			implementation.WriteLine ("}");
			implementation.WriteLine ();
		}

		void WriteDealloc ()
		{
			if (IsStatic || IsBaseTypeBound)
				return;
			implementation.WriteLine ("-(void) dealloc");
			implementation.WriteLine ("{");
			implementation.Indent++;
			implementation.WriteLine ("if (_object)");
			implementation.Indent++;
			implementation.WriteLine ("mono_embeddinator_destroy_object (_object);");
			implementation.Indent--;
			implementation.Indent--;
			implementation.WriteLine ("}");
			implementation.WriteLine ();
		}

		void ImplementGetGCHandle ()
		{
			implementation.WriteLine ("// for internal embeddinator use only");
			implementation.WriteLine ("- (GCHandle)xamarinGetGCHandle");
			implementation.WriteLine ("{");
			implementation.Indent++;
			implementation.WriteLine ("return _object->_handle;");
			implementation.Indent--;
			implementation.WriteLine ("}");
			implementation.WriteLine ();
		}

		protected void ImplementInitForSuper ()
		{
			implementation.WriteLine ("// for internal embeddinator use only");
			implementation.WriteLine ("- (nullable instancetype) initForSuper {");
			implementation.Indent++;
			// calls super's initForSuper until we reach a non-generated type
			if (IsBaseTypeBound)
				implementation.WriteLine ("return self = [super initForSuper];");
			else
				implementation.WriteLine ("return self = [super init];");
			implementation.Indent--;
			implementation.WriteLine ("}");
			implementation.WriteLine ();
		}

		public void EndImplementation ()
		{
			if (!IsStatic) {
				ImplementGetGCHandle ();
				ImplementInitForSuper ();
			}
			implementation.WriteLine ("@end");
			implementation.WriteLine ();
		}
	}
}
