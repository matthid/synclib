// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.SyncLib.Ui

open Mono.Unix

/// Simple module for the Mono.Unix.Catalog class
module PosixHelper = 
    let catalogInit package localedir = 
        Catalog.Init (package, localedir)
    /// Gets a string from the catalogue
    let CString s = 
        Catalog.GetString s
    /// Gets a plural string from the catalogue
    let CPluralString singular plural n = 
        Catalog.GetPluralString(singular, plural, n)