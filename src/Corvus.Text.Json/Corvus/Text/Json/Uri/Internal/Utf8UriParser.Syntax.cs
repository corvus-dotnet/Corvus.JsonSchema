// <copyright file="Utf8UriParser.Syntax.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Collections;

namespace Corvus.Text.Json.Internal;

internal partial class Utf8UriParser
{
    internal const int NoDefaultPort = -1;

    // These are always available without paying hashtable lookup cost
    // Note: see UpdateStaticSyntaxReference()
    internal static readonly Utf8UriParser FileUri = new BuiltInUriParser("file", NoDefaultPort, FileSyntaxFlags);

    internal static readonly Utf8UriParser FtpUri = new BuiltInUriParser("ftp", 21, FtpSyntaxFlags);

    internal static readonly Utf8UriParser GopherUri = new BuiltInUriParser("gopher", 70, GopherSyntaxFlags);

    internal static readonly Utf8UriParser HttpUri = new BuiltInUriParser("http", 80, HttpSyntaxFlags);

    internal static readonly Utf8UriParser HttpsUri = new BuiltInUriParser("https", 443, HttpUri._flags);

    internal static readonly Utf8UriParser LdapUri = new BuiltInUriParser("ldap", 389, LdapSyntaxFlags);

    internal static readonly Utf8UriParser MailToUri = new BuiltInUriParser("mailto", 25, MailtoSyntaxFlags);

    internal static readonly Utf8UriParser NetPipeUri = new BuiltInUriParser("net.pipe", NoDefaultPort, NetPipeSyntaxFlags);

    internal static readonly Utf8UriParser NetTcpUri = new BuiltInUriParser("net.tcp", 808, NetTcpSyntaxFlags);

    internal static readonly Utf8UriParser NewsUri = new BuiltInUriParser("news", NoDefaultPort, NewsSyntaxFlags);

    internal static readonly Utf8UriParser NntpUri = new BuiltInUriParser("nntp", 119, NntpSyntaxFlags);

    internal static readonly Utf8UriParser TelnetUri = new BuiltInUriParser("telnet", 23, TelnetSyntaxFlags);

    internal static readonly Utf8UriParser UnixFileUri = new BuiltInUriParser("file", NoDefaultPort, UnixFileSyntaxFlags);

    internal static readonly Utf8UriParser UuidUri = new BuiltInUriParser("uuid", NoDefaultPort, NewsUri._flags);

    internal static readonly Utf8UriParser VsMacrosUri = new BuiltInUriParser("vsmacros", NoDefaultPort, VsmacrosSyntaxFlags);

    internal static readonly Utf8UriParser WssUri = new BuiltInUriParser("wss", 443, HttpSyntaxFlags);

    internal static readonly Utf8UriParser WsUri = new BuiltInUriParser("ws", 80, HttpSyntaxFlags);

    private const int c_InitialTableSize = 25;

    private const int c_MaxCapacity = 512;

    private const Utf8UriSyntaxFlags FileSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.AllowEmptyHost |
                                    Utf8UriSyntaxFlags.AllowUncHost |
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |
                                    Utf8UriSyntaxFlags.MayHaveQuery |

                                    Utf8UriSyntaxFlags.FileLikeUri |

                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.AllowDOSPath |

                                    Utf8UriSyntaxFlags.ConvertPathSlashes |
                                    Utf8UriSyntaxFlags.CompressPath |
                                    Utf8UriSyntaxFlags.CanonicalizeAsFilePath |
                                    Utf8UriSyntaxFlags.UnEscapeDotsAndSlashes |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags FtpSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.PathIsRooted |

                                    Utf8UriSyntaxFlags.ConvertPathSlashes |
                                    Utf8UriSyntaxFlags.CompressPath |
                                    Utf8UriSyntaxFlags.CanonicalizeAsFilePath |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags GopherSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags HttpSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveQuery |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.PathIsRooted |

                                    Utf8UriSyntaxFlags.ConvertPathSlashes |
                                    Utf8UriSyntaxFlags.CompressPath |
                                    Utf8UriSyntaxFlags.CanonicalizeAsFilePath |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags LdapSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.AllowEmptyHost |
                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveQuery |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags MailtoSyntaxFlags =

                                    Utf8UriSyntaxFlags.AllowEmptyHost |
                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |
                                    Utf8UriSyntaxFlags.MayHaveQuery | // to maintain compat

                                    Utf8UriSyntaxFlags.MailToLikeUri |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags NetPipeSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveQuery |
                                    Utf8UriSyntaxFlags.MayHaveFragment |
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |
                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.ConvertPathSlashes |
                                    Utf8UriSyntaxFlags.CompressPath |
                                    Utf8UriSyntaxFlags.CanonicalizeAsFilePath |
                                    Utf8UriSyntaxFlags.UnEscapeDotsAndSlashes |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags NetTcpSyntaxFlags = NetPipeSyntaxFlags | Utf8UriSyntaxFlags.MayHavePort;

    // Note that NNTP and NEWS are quite different in syntax
    private const Utf8UriSyntaxFlags NewsSyntaxFlags =
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags NntpSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags TelnetSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.MayHaveUserInfo |
                                    Utf8UriSyntaxFlags.MayHavePort |
                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.PathIsRooted |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags UnixFileSyntaxFlags =
                                    FileSyntaxFlags & ~Utf8UriSyntaxFlags.ConvertPathSlashes;

    // Various Uri scheme syntax flags
    private const Utf8UriSyntaxFlags UnknownV1SyntaxFlags =
                                        Utf8UriSyntaxFlags.V1_UnknownUri | // This flag must be always set here
                                        Utf8UriSyntaxFlags.OptionalAuthority |

                                        Utf8UriSyntaxFlags.MayHaveUserInfo |
                                        Utf8UriSyntaxFlags.MayHavePort |
                                        Utf8UriSyntaxFlags.MayHavePath |
                                        Utf8UriSyntaxFlags.MayHaveQuery |
                                        Utf8UriSyntaxFlags.MayHaveFragment |

                                        Utf8UriSyntaxFlags.AllowEmptyHost |
                                        Utf8UriSyntaxFlags.AllowUncHost |       // V1 compat
                                        Utf8UriSyntaxFlags.AllowAnInternetHost |
                                        Utf8UriSyntaxFlags.PathIsRooted |
                                        Utf8UriSyntaxFlags.AllowDOSPath |        // V1 compat, actually we should not parse DOS file out of an unknown scheme
                                        Utf8UriSyntaxFlags.ConvertPathSlashes |  // V1 compat, it will always convert backslashes
                                        Utf8UriSyntaxFlags.CompressPath |        // V1 compat, it will always compress path even for non hierarchical Uris
                                        Utf8UriSyntaxFlags.AllowIdn |
                                        Utf8UriSyntaxFlags.AllowIriParsing;

    private const Utf8UriSyntaxFlags VsmacrosSyntaxFlags =
                                    Utf8UriSyntaxFlags.MustHaveAuthority |

                                    Utf8UriSyntaxFlags.AllowEmptyHost |
                                    Utf8UriSyntaxFlags.AllowUncHost |
                                    Utf8UriSyntaxFlags.AllowAnInternetHost |

                                    Utf8UriSyntaxFlags.MayHavePath |
                                    Utf8UriSyntaxFlags.MayHaveFragment |

                                    Utf8UriSyntaxFlags.FileLikeUri |

                                    Utf8UriSyntaxFlags.AllowDOSPath |
                                    Utf8UriSyntaxFlags.ConvertPathSlashes |
                                    Utf8UriSyntaxFlags.CompressPath |
                                    Utf8UriSyntaxFlags.CanonicalizeAsFilePath |
                                    Utf8UriSyntaxFlags.UnEscapeDotsAndSlashes |
                                    Utf8UriSyntaxFlags.AllowIdn |
                                    Utf8UriSyntaxFlags.AllowIriParsing;

    private static readonly Hashtable s_table = new(16) // Hashtable used instead of Dictionary<> for lock-free reads
    {
        { HttpUri.SchemeName, HttpUri }, // HTTP
        { HttpsUri.SchemeName, HttpsUri }, // HTTPS cloned from HTTP
        { WsUri.SchemeName, WsUri }, // WebSockets
        { WssUri.SchemeName, WssUri }, // Secure WebSockets
        { FtpUri.SchemeName, FtpUri }, // FTP
        { FileUri.SchemeName, FileUri }, // FILE
        { GopherUri.SchemeName, GopherUri }, // GOPHER
        { NntpUri.SchemeName, NntpUri }, // NNTP
        { NewsUri.SchemeName, NewsUri }, // NEWS
        { MailToUri.SchemeName, MailToUri }, // MAILTO
        { UuidUri.SchemeName, UuidUri }, // UUID cloned from NEWS
        { TelnetUri.SchemeName, TelnetUri }, // TELNET
        { LdapUri.SchemeName, LdapUri }, // LDAP
        { NetTcpUri.SchemeName, NetTcpUri },
        { NetPipeUri.SchemeName, NetPipeUri },
        { VsMacrosUri.SchemeName, VsMacrosUri }, // VSMACROS
    };

    private static Hashtable s_tempTable = new(c_InitialTableSize); // Hashtable used instead of Dictionary<> for lock-free reads

    private Utf8UriSyntaxFlags _flags;

    private int _port;

    private string _scheme;

    // Internal .ctor, any ctor eventually goes through this one
    internal Utf8UriParser(Utf8UriSyntaxFlags flags)
    {
        _flags = flags;
        _scheme = string.Empty;
    }

    internal Utf8UriSyntaxFlags Flags
    {
        get
        {
            return _flags;
        }
    }

    internal static Utf8UriParser FindOrFetchAsUnknownV1Syntax(string lwrCaseScheme)
    {
        // check may be other thread just added one
        var syntax = (Utf8UriParser?)s_table[lwrCaseScheme];
        if (syntax != null)
        {
            return syntax;
        }

        syntax = (Utf8UriParser?)s_tempTable[lwrCaseScheme];
        if (syntax != null)
        {
            return syntax;
        }

        lock (s_table)
        {
            if (s_tempTable.Count >= c_MaxCapacity)
            {
                s_tempTable = new Hashtable(c_InitialTableSize);
            }

            syntax = new BuiltInUriParser(lwrCaseScheme, NoDefaultPort, UnknownV1SyntaxFlags);
            s_tempTable[lwrCaseScheme] = syntax;
            return syntax;
        }
    }

    internal bool InFact(Utf8UriSyntaxFlags flags)
    {
        // Return true if at least one of the flags in 'flags' is set.
        return !IsFullMatch(flags, Utf8UriSyntaxFlags.None);
    }

    internal bool IsAllSet(Utf8UriSyntaxFlags flags)
    {
        // Return true if all flags in 'flags' are set.
        return IsFullMatch(flags, flags);
    }

    internal bool NotAny(Utf8UriSyntaxFlags flags)
    {
        // Return true if none of the flags specified in 'flags' are set.
        return IsFullMatch(flags, Utf8UriSyntaxFlags.None);
    }

    private bool IsFullMatch(Utf8UriSyntaxFlags flags, Utf8UriSyntaxFlags expected)
    {
        return (_flags & flags) == expected;
    }
}