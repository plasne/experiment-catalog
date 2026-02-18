I want to add status tracking to this so it is clear to a user where we are in the job, how many errors we have had, how many items were successful, and when the job is done.

It is typical that there are thousands of work items in a job and maybe a dozen jobs per day. There are commonly quite a few Evaluators running the inference and evaluation roles. There is at least 1 running the API role, but it could be more than one.

Originally I thought using App Insights would be a good solution to keep track of what was pending and done, but it wasn't reliable (often missing messages even though things were processing successfully).

I don't want to use a database because I want to have minimal dependencies. Right now this solution only requires a storage account.

I am thinking the job information could be stored in another blob container. I am thinking there would be an append blob for each job which has metadata for the total number of work items. Then as work items are successful or failed they are written to the blob. Then when the list of jobs is queried for, a list of blobs is returned. The metadata would also be read in that same query so we get the number of total items, number of successful inference, successful evaluation, failed inference, and failed evaluation. Those last 4 properties would only be written to the metadata once the blob had all the records finished. If those weren't populated in the metadata, the list job would have to open the blob, interrogate the messages, and tally the results. If done, the results would be written to the metadata.

Since this solution uses a queue with retries, it is possible that the same work item (determined by ID) could be processed multiple times. So the logic that tallies the results would need to be smart enough to only count the final successful or failed result for a given work item ID.

The job would be considered done when the total number of successful inference + failed inference equals the total number of work items AND the total number of successful evaluation + failed evaluation equals the total number of work items. Alternatively, there should be a timeout setting. If the last record in the blob is older than the timeout, then the job is considered done and the results are tallied based on the records in the blob.

Initially I am not excited about using Table storage since its a bunch of data that would never be deleted. Job data in blobs would be very easy to clean up.
