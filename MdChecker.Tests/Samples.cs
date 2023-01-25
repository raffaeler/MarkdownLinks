﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MdChecker.Tests;

internal static class Samples
{
    public static string Document1 = """"

                # Document1

        This is a sample document to verify the correct extraction of the links.

        [Lorem ipsum](https://loremipsum.io/generator/?n=5&t=p) dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Sit amet risus nullam eget felis. Suspendisse sed nisi lacus sed viverra. Netus et malesuada fames ac turpis egestas sed tempus. Potenti nullam ac tortor vitae. Nulla aliquet enim tortor at auctor urna nunc id. Cum sociis natoque penatibus et magnis dis parturient montes. Tellus pellentesque eu tincidunt tortor. Nisi scelerisque eu ultrices vitae auctor eu augue ut lectus. In pellentesque massa placerat duis ultricies lacus. Non tellus orci ac auctor augue mauris augue. [Quis imperdiet massa](http://ww1.microchip.com/downloads/en/devicedoc/21295c.pdf) tincidunt nunc pulvinar. Quis enim lobortis scelerisque fermentum dui faucibus in ornare quam. Consequat nisl vel pretium lectus quam.

        Ipsum nunc [aliquet](https://xyzdoesnotexist.com/blah) bibendum enim facilisis gravida neque. Nibh tortor id aliquet lectus proin nibh nisl condimentum id. Lorem mollis aliquam ut porttitor leo a. Odio euismod lacinia at quis risus. Cursus in hac habitasse platea dictumst quisque. Tincidunt vitae semper quis lectus nulla at volutpat diam. [Felis eget](./Document1.md) nunc lobortis mattis. Scelerisque eleifend donec pretium vulputate sapien nec sagittis aliquam malesuada. Interdum varius sit amet mattis vulputate enim nulla. Gravida dictum fusce ut placerat orci nulla pellentesque dignissim enim. Nunc scelerisque viverra mauris in. Quis enim lobortis scelerisque fermentum dui. Est placerat in egestas erat imperdiet sed euismod nisi porta. Semper eget duis at tellus. Ac feugiat sed lectus vestibulum mattis ullamcorper velit sed. Sit amet mattis vulputate enim nulla aliquet porttitor lacus luctus. Integer feugiat scelerisque varius morbi enim nunc. Sem et tortor consequat id porta nibh venenatis. In aliquam sem fringilla ut morbi tincidunt augue. Sapien nec sagittis aliquam malesuada bibendum arcu vitae.

        Ante in nibh mauris cursus [mattis](https://www.hanselman.com/blog/RemoteDebuggingWithVSCodeOnWindowsToARaspberryPiUsingNETCoreOnARM.aspx) molestie a iaculis at. Nisl condimentum id venenatis a condimentum vitae sapien pellentesque. Nulla aliquet enim tortor at auctor urna. Quisque egestas diam in arcu cursus euismod quis. Vitae et leo duis ut diam. Libero nunc consequat interdum varius sit amet. Sit amet consectetur adipiscing elit ut aliquam purus sit. Ullamcorper a lacus vestibulum sed arcu non odio euismod lacinia. Ornare suspendisse sed nisi lacus sed viverra. Volutpat lacus laoreet non curabitur. Id neque aliquam vestibulum morbi blandit cursus risus at. Amet commodo nulla facilisi nullam vehicula ipsum a arcu. Tristique senectus et netus et. Et netus et malesuada fames ac turpis egestas. Ac ut consequat semper viverra nam libero justo. Ipsum consequat nisl vel pretium lectus quam id leo in. Vitae tempus quam pellentesque nec nam aliquam sem.

        Aenean sed adipiscing diam donec adipiscing tristique. Mauris ultrices eros in cursus turpis massa tincidunt. Ut tortor pretium viverra suspendisse potenti nullam ac. Dolor magna eget est lorem ipsum dolor sit. Nec ullamcorper sit amet risus nullam eget. Id interdum velit laoreet id donec. Faucibus nisl tincidunt eget nullam non nisi. Mus mauris vitae ultricies leo. Ut eu sem integer vitae. In tellus integer feugiat scelerisque varius morbi enim. Purus gravida quis blandit turpis cursus. Risus in hendrerit gravida rutrum quisque non tellus orci ac. Sagittis eu volutpat odio facilisis mauris sit. Ultricies tristique nulla aliquet enim tortor at auctor urna nunc. Lorem ipsum dolor sit amet. Dignissim convallis aenean et tortor at risus.

        | Some data       | Link                          | Other data                                    |
        | --------------- | ----------------------------- | --------------------------------------------- |
        | Microsoft       | https://www.microsoft.com     | [Blogs](https://blogs.microsoft.com/)         |
        | .NET Foundation | https://dotnetfoundation.org/ | [FAQ](https://dotnetfoundation.org/about/faq) |
        | Github          | https://github.com/           | [Blog](https://github.blog/)                  |

        Consequat nisl vel pretium lectus quam id. Dolor purus non enim praesent elementum. Orci porta non pulvinar neque laoreet suspendisse interdum consectetur libero. Quisque id diam vel quam elementum pulvinar etiam non quam. Dignissim cras tincidunt lobortis feugiat vivamus. In eu mi bibendum neque egestas congue quisque. Mauris sit amet massa vitae tortor condimentum. Nunc non blandit massa enim nec dui. Purus sit amet luctus venenatis lectus magna fringilla urna. Arcu vitae elementum curabitur vitae nunc sed velit dignissim. Risus viverra adipiscing at in tellus. Commodo quis imperdiet massa tincidunt nunc pulvinar sapien et. Et sollicitudin ac orci phasellus egestas tellus rutrum. Vitae congue mauris rhoncus aenean vel elit scelerisque mauris.
        """";

    public static List<Hyperlink> ResultsOk1 = new()
    {
        new Hyperlink(string.Empty, 5, "https://loremipsum.io/generator/?n=5&t=p", true),
        new Hyperlink(string.Empty, 5, "http://ww1.microchip.com/downloads/en/devicedoc/21295c.pdf", true),
        new Hyperlink(string.Empty, 9, "https://www.hanselman.com/blog/RemoteDebuggingWithVSCodeOnWindowsToARaspberryPiUsingNETCoreOnARM.aspx", true),
        new Hyperlink(string.Empty, 15, "https://www.microsoft.com", true),
        new Hyperlink(string.Empty, 15, "https://blogs.microsoft.com/", true),
        new Hyperlink(string.Empty, 16, "https://dotnetfoundation.org/", true),
        new Hyperlink(string.Empty, 16, "https://dotnetfoundation.org/about/faq", true),
        new Hyperlink(string.Empty, 17, "https://github.com/", true),
        new Hyperlink(string.Empty, 17, "https://github.blog/", true),
    };

    public static List<Hyperlink> ResultsFail1 = new()
    {
        new Hyperlink(string.Empty, 7, "https://xyzdoesnotexist.com/blah", true),
        new Hyperlink(string.Empty, 7, "./Document1.md", false),
    };

}
