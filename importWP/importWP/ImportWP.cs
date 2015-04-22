/* -----------------------------------------------------------------
 * Copyright (c) 2015 Robert Adams
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
 *     * Neither the name of the author nor the names of
 *       contributors may be used to endorse or promote products derived
 *       from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHOR OR
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
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

using MySql.Data.MySqlClient;

using ParameterParsing;
using Logging;

namespace importWP
{
class ImportWP
{
    Dictionary<string, string> m_Parameters;
    int m_verbose = 0;

    string m_dbHost;
    string m_dbName;
    string m_dbUser;
    string m_dbPass;
    
    private string Invocation()
    {
        return @"Invocation:
INVOCATION:
ImportWP 
        -H|--dbHost databaseHost
        -D|--dbName databaseName
        -U|--dbUser databaseUser
        -P|--dbPass databaseUserPassword
        -o|--output outputFilename
        --verbose
";
    }

    static void Main(string[] args)
    {
        ImportWP prog = new ImportWP();
        prog.Start(args);
        return;
    }

    public ImportWP()
    {
    }

    public void Start(string[] args)
    {
        m_Parameters = ParameterParse.ParseArguments(args, false /* firstOpFlag */, true /* multipleFiles */);
        foreach (KeyValuePair<string, string> kvp in m_Parameters)
        {
            switch (kvp.Key)
            {
                case "-H":
                case "--dbHost":
                    m_dbHost = kvp.Value;
                    break;
                case "-D":
                case "--dbName":
                    m_dbName = kvp.Value;
                    break;
                case "-U":
                case "--dbUser":
                    m_dbUser = kvp.Value;
                    break;
                case "-P":
                case "--dbPass":
                    m_dbPass = kvp.Value;
                    break;
                case "--verbose":
                    m_verbose++;
                    break;
                // case ParameterParse.LAST_PARAM:
                //     break;
                case ParameterParse.ERROR_PARAM:
                    // if we get here, the parser found an error
                    Logger.Log("Parameter error: " + kvp.Value);
                    Logger.Log(Invocation());
                    return;
                default:
                    Logger.Log("ERROR: UNKNOWN PARAMETER: " + kvp.Key);
                    Logger.Log(Invocation());
                    return;
            }
        }

        string m_connectionString = String.Format("Data Source={0};Database={1};User ID={2};Password={3}",
                                            m_dbHost, m_dbName, m_dbUser, m_dbPass);
        Logger.Log("Connection string = {0}", m_connectionString);

        Dictionary<ulong, Dictionary<string, string>> pageNameList = new Dictionary<ulong,Dictionary<string,string>>();
        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
        {
            dbcon.Open();

            // Find the Pages
            using (MySqlCommand cmd = new MySqlCommand(
                       "SELECT ID, post_title, post_name, post_parent FROM wp_posts WHERE post_type = 'page'", dbcon))
            {
                try
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            ulong pageID = (ulong)dbReader["ID"];
                            string pageTitle = (string)dbReader["post_title"];
                            string pageSlug = sluggify((string)dbReader["post_name"]);
                            ulong pageParent = (ulong)dbReader["post_parent"];
                            Dictionary<string, string> pieces = new Dictionary<string,string>();
                            pieces["pageTitle"] = pageTitle;
                            pieces["pageSlug"] = pageSlug;
                            pieces["pageParent"] = pageParent.ToString();
                            pageNameList[pageID] = pieces;

                            Logger.Log("Pages: ID={0}, title={1}, slug={2}, parent={3}",
                                                    pageID, pageTitle, pageSlug, pageParent);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Error reading DB: {0}", e);
                }
            }


            using (MySqlCommand cmd = new MySqlCommand(
                               "SELECT "
                                 + "wp_posts.ID,"
                                 + "wp_posts.guid,"
                                 + "wp_posts.post_type,"
                                 + "wp_posts.post_status,"
                                 + "wp_posts.post_title,"
                                 + "wp_posts.post_name,"
                                 + "wp_posts.post_date,"
                                 + "wp_posts.post_date_gmt,"
                                 + "wp_posts.post_content,"
                                 + "wp_posts.post_excerpt,"
                                 + "wp_posts.comment_count,"
                                 + "wp_users.display_name,"
                                 + "wp_users.user_login,"
                                 + "wp_users.user_email,"
                                 + "wp_users.user_url"
                                 + " FROM wp_posts LEFT JOIN wp_users ON wp_posts.post_author = wp_users.ID"
                                 + " WHERE wp_posts.post_status = 'publish'"
                                           , dbcon))
            {
                try
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            ulong postID = (ulong)dbReader["ID"];
                            string postTitle = (string)dbReader["post_title"];

                            Logger.Log("Post: ID={0}, title={1}", postID, postTitle);
                            ProcessPost(dbReader);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Error reading DB: {0}", e);
                }
            }
        }


    }

    private void ProcessPost(IDataReader thePost)
    {
    }

    // Turn a title into a URLable slug
    //  def self.sluggify( title )
    //    title = title.to_ascii.downcase.gsub(/[^0-9A-Za-z]+/, " ").strip.gsub(" ", "-")
    //  end
    private string sluggify(string title)
    {
        string ret = title.ToLower();
        ret = ret.Replace("[^0-9A-Za-z]+", " ");
        ret = ret.Trim();
        ret = ret.Replace(" ", "-");
        return ret;
    }

}
}
