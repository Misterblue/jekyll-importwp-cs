/* -----------------------------------------------------------------
 * Copyright (c) 2013 Intel Corporation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *
 *     * Redistributions in binary form must reproduce the above
 *       copyright notice, this list of conditions and the following
 *       disclaimer in the documentation and/or other materials provided
 *       with the distribution.
 *
 *     * Neither the name of the Intel Corporation nor the names of its
 *       contributors may be used to endorse or promote products derived
 *       from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE INTEL OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * EXPORT LAWS: THIS LICENSE ADDS NO RESTRICTIONS TO THE EXPORT LAWS OF
 * YOUR JURISDICTION. It is licensee's responsibility to comply with any
 * export regulations applicable in licensee's jurisdiction. Under
 * CURRENT (May 2000) U.S. export regulations this software is eligible
 * for export from the U.S. and can be downloaded by or otherwise
 * exported or reexported worldwide EXCEPT to U.S. embargoed destinations
 * which include Cuba, Iraq, Libya, North Korea, Iran, Syria, Sudan,
 * Afghanistan and any other country to which the U.S. has embargoed
 * goods and services.
 * -----------------------------------------------------------------
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace ParameterParsing
{
public static class ParameterParse
{
    public const string FIRST_PARAM = "--firstParameter";
    public const string LAST_PARAM = "--lastParameter";
    public const string ERROR_PARAM = "--errorParameter";
    
    // ================================================================
    /// <summary>
    /// Given the array of command line arguments, create a dictionary of the parameter
    /// keyword to values. If there is no value for a parameter keyword, the value of
    /// 'null' is stored.
    /// Command line keywords begin with "-" or "--". Anything else is presumed to be
    /// a value.
    /// </summary>
    /// <param name="args">array of command line tokens</param>
    /// <param name="firstOpFlag">if 'true' presume the first token in the parameter line
    /// is a special value that should be assigned to the keyword "--firstparam".</param>
    /// <param name="multipleFiles">if 'true' presume multiple specs at the end of the line
    /// are filenames and pack them together into a CSV string in LAST_PARAM.</param>
    /// <returns></returns>
    public static Dictionary<string, string> ParseArguments(string[] args, bool firstOpFlag, bool multipleFiles)
    {
        Dictionary<string, string> m_params = new Dictionary<string, string>();

        for (int ii = 0; ii < args.Length; ii++)
        {
            string para = args[ii];
            if (para[0] == '-')     // is this a parameter?
            {
                if (ii == (args.Length - 1) || args[ii + 1][0] == '-') // is the next one a parameter?
                {
                    // two parameters in a row. this must be a toggle parameter
                    m_params.Add(para, null);
                }
                else
                {
                    // looks like a parameter followed by a value
                    m_params.Add(para, args[ii + 1]);
                    ii++;       // skip the value we just added to the dictionary
                }
            }
            else
            {
                if (ii == 0 && firstOpFlag)
                {   // if the first thing is not a parameter, make like it's an op or something
                    m_params.Add(FIRST_PARAM, para);
                }
                else
                {
                    if (multipleFiles)
                    {
                        // Pack all remaining arguments into a comma-separated list as LAST_PARAM
                        StringBuilder multFiles = new StringBuilder();
                        for (int jj = ii; jj < args.Length; jj++)
                        {
                            if (multFiles.Length != 0)
                            {
                                multFiles.Append(",");
                            }
                            multFiles.Append(args[jj]);
                        }
                        m_params.Add(LAST_PARAM, multFiles.ToString());

                        // Skip them all
                        ii = args.Length;
                    }
                    else
                    {
                        // This token is not a keyword. If it's the last thing, place it
                        // into the dictionary as the last parameter. Otherwise an error.
                        if (ii == args.Length - 1)
                        {
                            m_params.Add(LAST_PARAM, para);
                        }
                        else
                        {
                            // something is wrong with  the format of the parameters
                            m_params.Add(ERROR_PARAM, "Unknown parameter " + para);
                        }
                    }
                }
            }
        }
        return m_params;
    }
}
}
